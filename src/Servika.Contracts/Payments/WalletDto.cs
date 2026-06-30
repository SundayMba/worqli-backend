namespace Servika.Contracts.Payments;

/// <summary>
/// The signed-in user's wallet balance (GET /api/v1/wallet), computed from the
/// append-only ledger — never trusted from the client.
/// </summary>
public sealed record WalletDto(int BalanceNaira, string Currency);
