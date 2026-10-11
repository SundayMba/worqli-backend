using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>The signed-in user's wallet ledger history, newest first.</summary>
public sealed class GetWalletTransactionsHandler
{
    private readonly IWalletRepository _wallet;

    public GetWalletTransactionsHandler(IWalletRepository wallet)
    {
        _wallet = wallet;
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> HandleAsync(
        Guid userId, CancellationToken ct)
    {
        var txns = await _wallet.ListForOwnerAsync(WalletOwnerType.Customer, userId, ct);
        return txns.Select(t => t.ToDto()).ToList();
    }
}

/// <summary>The signed-in artisan's own ledger (their profile's entries), newest
/// first, each with a plain-language description for the Earnings screen.</summary>
public sealed class GetArtisanWalletTransactionsHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IWalletRepository _wallet;

    public GetArtisanWalletTransactionsHandler(ICatalogueRepository catalogue, IWalletRepository wallet)
    {
        _catalogue = catalogue;
        _wallet = wallet;
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new Common.NotFoundException("No artisan profile is linked to this account.");
        var txns = await _wallet.ListForOwnerAsync(WalletOwnerType.Artisan, profile.Id, ct);
        return txns
            .Select(t => new WalletTransactionDto(
                t.Id, t.Type.ToString(), t.AmountNaira, t.BookingId, Describe(t), t.CreatedAt))
            .ToList();
    }

    /// <summary>Human wording per entry type (the stored description is internal).</summary>
    internal static string Describe(WalletTransaction t) => t.Type switch
    {
        WalletTransactionType.ArtisanEarning => "Earnings released for a completed job",
        WalletTransactionType.MaterialsAdvance => "Materials money released by the customer",
        WalletTransactionType.PayoutRequest => "Withdrawal to your bank",
        WalletTransactionType.PayoutCompleted => "Withdrawal completed",
        WalletTransactionType.Adjustment when t.Description.Contains("failed payout", StringComparison.OrdinalIgnoreCase)
            => "Withdrawal reversed: the money is back in your balance",
        WalletTransactionType.Adjustment when t.Description.Contains("refund", StringComparison.OrdinalIgnoreCase)
            => "Earnings returned after a customer refund",
        WalletTransactionType.Adjustment => "Balance adjustment",
        WalletTransactionType.CommissionDue => "Servika service fee on a cash job",
        WalletTransactionType.CommissionSettlement => "Service fees settled",
        WalletTransactionType.TransferFee => "Bank transfer charge",
        WalletTransactionType.Refund => "Refund",
        WalletTransactionType.ReferralBonus => "Referral reward",
        WalletTransactionType.ServiceFee => "Payment fee",
        WalletTransactionType.PlatformCommission => "Servika commission",
        WalletTransactionType.BookingPayment => "Booking payment",
        WalletTransactionType.GatewayCost => "Gateway charge",
        _ => t.Description,
    };
}
