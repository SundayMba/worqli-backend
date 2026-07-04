namespace Servika.Contracts.Payments;

/// <summary>
/// One artisan payout in the withdrawals history / the result of requesting one
/// (GET + POST /api/v1/artisan/withdrawals). <see cref="Status"/> is a readable
/// string ("Pending" / "Paid" / "Failed"); the account number is masked.
/// </summary>
public sealed record WithdrawalDto(
    Guid Id,
    int AmountNaira,
    string Status,
    string Method,
    string BankName,
    string AccountNumberMasked,
    string AccountName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAtUtc);

/// <summary>
/// Request an artisan payout (POST /api/v1/artisan/withdrawals). The amount is
/// validated against the artisan's available balance server-side.
/// </summary>
public sealed record RequestWithdrawalRequest(
    int AmountNaira,
    string BankName,
    string AccountNumber,
    string AccountName);
