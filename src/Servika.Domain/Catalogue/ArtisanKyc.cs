namespace Servika.Domain.Catalogue;

/// <summary>
/// An artisan's KYC submission — the basic identity proof needed to be verified:
/// a selfie (liveness / a real face on the profile) plus one government ID. The
/// image bytes live in file storage; this row holds their <b>keys</b> (so the
/// storage backend is swappable — local disk now, S3 later) and the review state.
///
/// <para>Reviewed manually for launch (admin Approve/Reject), but the decision is
/// produced by a verification provider port, so an automated NIN/liveness check
/// can replace the manual path without touching this entity.</para>
/// </summary>
public sealed class ArtisanKyc
{
    public Guid Id { get; private set; }

    /// <summary>The artisan's login account.</summary>
    public Guid UserId { get; private set; }

    public KycIdType IdType { get; private set; }

    /// <summary>ID number (e.g. NIN), optional — some artisans supply only a photo.</summary>
    public string? IdNumber { get; private set; }

    /// <summary>Storage key for the selfie image.</summary>
    public string SelfieKey { get; private set; } = string.Empty;

    /// <summary>Storage key for the ID document image.</summary>
    public string IdDocumentKey { get; private set; } = string.Empty;

    /// <summary>Review state (Pending → Verified/Rejected), same vocabulary as the profile.</summary>
    public ArtisanVerificationStatus Status { get; private set; }

    public string? ReviewNote { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    private ArtisanKyc() { }

    public static ArtisanKyc Submit(
        Guid userId,
        KycIdType idType,
        string? idNumber,
        string selfieKey,
        string idDocumentKey,
        DateTimeOffset now)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(selfieKey))
            throw new ArgumentException("A selfie is required.", nameof(selfieKey));
        if (string.IsNullOrWhiteSpace(idDocumentKey))
            throw new ArgumentException("An ID document is required.", nameof(idDocumentKey));

        return new ArtisanKyc
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IdType = idType,
            IdNumber = string.IsNullOrWhiteSpace(idNumber) ? null : idNumber.Trim(),
            SelfieKey = selfieKey,
            IdDocumentKey = idDocumentKey,
            Status = ArtisanVerificationStatus.Pending,
            SubmittedAtUtc = now,
        };
    }

    /// <summary>Re-submit new documents on a fresh review cycle (e.g. after rejection).</summary>
    public void Resubmit(
        KycIdType idType, string? idNumber, string selfieKey, string idDocumentKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(selfieKey))
            throw new ArgumentException("A selfie is required.", nameof(selfieKey));
        if (string.IsNullOrWhiteSpace(idDocumentKey))
            throw new ArgumentException("An ID document is required.", nameof(idDocumentKey));

        IdType = idType;
        IdNumber = string.IsNullOrWhiteSpace(idNumber) ? null : idNumber.Trim();
        SelfieKey = selfieKey;
        IdDocumentKey = idDocumentKey;
        Status = ArtisanVerificationStatus.Pending;
        ReviewNote = null;
        SubmittedAtUtc = now;
        ReviewedAtUtc = null;
    }

    public void Approve(DateTimeOffset now)
    {
        Status = ArtisanVerificationStatus.Verified;
        ReviewedAtUtc = now;
        ReviewNote = null;
    }

    public void Reject(string? reason, DateTimeOffset now)
    {
        Status = ArtisanVerificationStatus.Rejected;
        ReviewedAtUtc = now;
        ReviewNote = reason;
    }
}
