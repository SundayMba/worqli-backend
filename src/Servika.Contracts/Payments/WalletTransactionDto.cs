namespace Servika.Contracts.Payments;

/// <summary>
/// One ledger entry in the user's wallet history (GET /api/v1/wallet/transactions).
/// <see cref="AmountNaira"/> is signed from the owner's perspective (credit +,
/// debit −); <see cref="Type"/> is a readable string.
/// </summary>
public sealed record WalletTransactionDto(
    Guid Id,
    string Type,
    int AmountNaira,
    Guid? BookingId,
    string Description,
    DateTimeOffset CreatedAt);
