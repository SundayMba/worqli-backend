namespace Servika.Contracts.Admin;

/// <summary>Admin payments & commissions analytics, aggregated from the wallet ledger.</summary>
public sealed record AdminPaymentsDto(
    long TotalRevenueNaira,
    long CommissionEarnedNaira,
    long ArtisanEarningsNaira,
    long PendingPayoutsNaira,
    long RefundAmountNaira,
    PayoutSummaryDto Payout,
    IReadOnlyList<RevenuePointDto> RevenueSeries,
    IReadOnlyList<LedgerEntryDto> RecentTransactions,
    /// <summary>Refunds that failed at the gateway or are still settling — the ones
    /// an admin may need to chase. Empty when everything has landed.</summary>
    IReadOnlyList<AdminRefundDto> RefundsNeedingAttention);

/// <summary>A refund's settlement state for the admin refunds view.</summary>
public sealed record AdminRefundDto(
    Guid BookingId,
    int AmountNaira,
    /// <summary>"Failed" (gateway refused, needs a manual retry) or "Pending"
    /// (requested, not yet confirmed by the provider).</summary>
    string Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? FailedAtUtc);

/// <summary>Payout totals by status (for the donut).</summary>
public sealed record PayoutSummaryDto(
    long CompletedNaira,
    long PendingNaira,
    long FailedNaira,
    long TotalNaira,
    int CompletedCount,
    int PendingCount);

/// <summary>One day of revenue for the overview chart.</summary>
public sealed record RevenuePointDto(string Date, long AmountNaira);

/// <summary>One wallet-ledger entry for the recent-transactions list.</summary>
public sealed record LedgerEntryDto(
    Guid Id,
    string Type,
    string Description,
    int AmountNaira,
    DateTimeOffset CreatedAt,
    Guid? BookingId);
