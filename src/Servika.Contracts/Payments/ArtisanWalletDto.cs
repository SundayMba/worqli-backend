namespace Servika.Contracts.Payments;

/// <summary>
/// An artisan's earnings summary (GET /api/v1/artisan/wallet), all computed from
/// the append-only ledger — never trusted from the client. <see cref="AvailableNaira"/>
/// is what they can withdraw now (earnings minus payouts already reserved/paid).
/// </summary>
public sealed record ArtisanWalletDto(
    int AvailableNaira,
    int TotalEarnedNaira,
    int TotalWithdrawnNaira,
    string Currency);
