namespace Servika.Domain.Payments;

/// <summary>
/// Whose ledger a wallet transaction belongs to. A ledger entry is scoped to an
/// owner (type + id) so a balance is always the sum of one owner's entries.
/// </summary>
public enum WalletOwnerType
{
    Customer = 0,

    /// <summary>The artisan being paid. Until artisan profiles are linked to User
    /// accounts, the owner id is the artisan-profile id (earnings still accrue).</summary>
    Artisan = 1,

    /// <summary>Servika's own account (commission). Uses a fixed owner id.</summary>
    Platform = 2,

    /// <summary>A user's referral-reward pool (owner id = referrer's user id). Kept
    /// separate from their Customer wallet so referral cash is withdrawable on its
    /// own, independent of any booking-payment debits.</summary>
    Referrer = 3,
}
