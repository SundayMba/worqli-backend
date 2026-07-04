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
    IReadOnlyList<LedgerEntryDto> RecentTransactions);

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
