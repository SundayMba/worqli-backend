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
    string Currency,
    /// <summary>Unpaid cash-job service fees not yet covered by earnings —
    /// the amount to settle. 0 when earnings absorb the fees (auto-netting).</summary>
    int CommissionOwedNaira = 0,
    /// <summary>True when the owed amount is past the platform's debt limit —
    /// the artisan stops receiving new job requests until they settle.</summary>
    bool IsRestricted = false);
