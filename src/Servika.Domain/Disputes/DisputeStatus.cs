namespace Servika.Domain.Disputes;

/// <summary>
/// A dispute's lifecycle. Stored as a readable string.
/// <code>Open → UnderReview → Resolved</code>
/// The customer raises it (Open); an admin may acknowledge it (UnderReview) and
/// finally resolves it (Resolved) with a <see cref="DisputeResolution"/> outcome.
/// </summary>
public enum DisputeStatus
{
    /// <summary>Raised by the customer, awaiting an admin.</summary>
    Open = 0,

    /// <summary>An admin has picked it up and is investigating.</summary>
    UnderReview = 1,

    /// <summary>Closed with a decision (see <see cref="DisputeResolution"/>).</summary>
    Resolved = 2,
}
