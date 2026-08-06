using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Contracts.Admin;
using Servika.Domain.Payments;

namespace Servika.Application.Admin;

/// <summary>
/// Admin payments & commissions analytics, aggregated from the append-only wallet
/// ledger + the withdrawals table. Everything is derived (never stored twice):
/// revenue = gross booking payments, commission = the platform's cut, payouts =
/// withdrawals by status, and a daily revenue series for the chart.
/// </summary>
public sealed class GetAdminPaymentsHandler
{
    private const int SeriesDays = 14;

    private readonly IWalletRepository _wallet;
    private readonly IWithdrawalRepository _withdrawals;
    private readonly IPaymentRepository _payments;
    private readonly IClock _clock;

    public GetAdminPaymentsHandler(
        IWalletRepository wallet, IWithdrawalRepository withdrawals,
        IPaymentRepository payments, IClock clock)
    {
        _wallet = wallet;
        _withdrawals = withdrawals;
        _payments = payments;
        _clock = clock;
    }

    public async Task<AdminPaymentsDto> HandleAsync(CancellationToken ct)
    {
        var entries = await _wallet.ListAllAsync(ct);
        var payouts = await _withdrawals.ListAllAsync(ct);

        // BookingPayment is a customer debit (negative); revenue is its magnitude.
        long revenue = entries.Where(e => e.Type == WalletTransactionType.BookingPayment).Sum(e => (long)-e.AmountNaira);
        long commission = entries.Where(e => e.Type == WalletTransactionType.PlatformCommission).Sum(e => (long)e.AmountNaira);
        long artisanEarnings = entries.Where(e => e.Type == WalletTransactionType.ArtisanEarning).Sum(e => (long)e.AmountNaira);
        long refunds = entries.Where(e => e.Type == WalletTransactionType.Refund).Sum(e => (long)Math.Abs(e.AmountNaira));

        long completed = payouts.Where(p => p.Status == WithdrawalStatus.Paid).Sum(p => (long)p.AmountNaira);
        long pending = payouts.Where(p => p.Status == WithdrawalStatus.Pending).Sum(p => (long)p.AmountNaira);
        long failed = payouts.Where(p => p.Status == WithdrawalStatus.Failed).Sum(p => (long)p.AmountNaira);
        var payoutSummary = new PayoutSummaryDto(
            completed, pending, failed, completed + pending + failed,
            payouts.Count(p => p.Status == WithdrawalStatus.Paid),
            payouts.Count(p => p.Status == WithdrawalStatus.Pending));

        // Daily revenue series over the last SeriesDays days (oldest → newest).
        var today = _clock.UtcNow.Date;
        var series = new List<RevenuePointDto>(SeriesDays);
        for (var i = SeriesDays - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            long amount = entries
                .Where(e => e.Type == WalletTransactionType.BookingPayment && e.CreatedAt.UtcDateTime.Date == day)
                .Sum(e => (long)-e.AmountNaira);
            series.Add(new RevenuePointDto(day.ToString("yyyy-MM-dd"), amount));
        }

        var recent = entries
            .Take(12)
            .Select(e => new LedgerEntryDto(e.Id, e.Type.ToString(), e.Description, e.AmountNaira, e.CreatedAt, e.BookingId))
            .ToList();

        // Refunds that failed at the gateway, or were requested and haven't been
        // confirmed settled yet — the ones an admin may need to chase. A refund
        // that landed (RefundSettledAtUtc set) needs no attention.
        var refundAttention = (await _payments.ListRefundedAsync(ct))
            .Where(p => p.RefundSettledAtUtc is null)
            .Select(p => new AdminRefundDto(
                p.BookingId ?? Guid.Empty,
                p.AmountNaira,
                p.RefundFailedAtUtc is not null ? "Failed" : "Pending",
                p.RefundedAtUtc!.Value,
                p.RefundFailedAtUtc))
            // Failed first (needs action), then oldest-requested first.
            .OrderByDescending(r => r.Status == "Failed")
            .ThenBy(r => r.RequestedAtUtc)
            .ToList();

        return new AdminPaymentsDto(
            revenue, commission, artisanEarnings, pending, refunds,
            payoutSummary, series, recent, refundAttention);
    }
}
