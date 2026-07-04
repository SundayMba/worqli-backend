namespace Servika.Domain.Disputes;

/// <summary>
/// The decision an admin reaches when resolving a dispute. Drives the booking's
/// terminal state: favouring the customer cancels the job (a refund would follow
/// in a later payments slice); favouring the artisan completes it.
/// </summary>
public enum DisputeResolution
{
    /// <summary>Not yet resolved.</summary>
    None = 0,

    /// <summary>Decided for the customer — booking is cancelled (refund follows later).</summary>
    FavourCustomer = 1,

    /// <summary>Decided for the artisan — the work stands, booking is completed.</summary>
    FavourArtisan = 2,
}
