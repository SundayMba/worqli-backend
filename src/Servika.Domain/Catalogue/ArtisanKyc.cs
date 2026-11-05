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

    /// <summary>JSON list of {pose, key}: the extra live selfies (head turned left / right).</summary>
    public string? PoseSelfiesJson { get; private set; }
    public void SetPoseSelfies(string? json) => PoseSelfiesJson = string.IsNullOrWhiteSpace(json) ? null : json;

    // ── Consent (NDPA 2023): recorded before any ID or face is collected ──
    /// <summary>Version of the consent text the artisan agreed to, e.g. "kyc-2026-11".</summary>
    public string? ConsentVersion { get; private set; }
    public DateTimeOffset? ConsentedAtUtc { get; private set; }

    // ── Liveness signals from the phone (the reviewer still decides) ──
    /// <summary>"on-device" (ML Kit face check ran) or "manual" (the phone could not run it).</summary>
    public string? LivenessMethod { get; private set; }
    public bool? LivenessPassed { get; private set; }
    /// <summary>Poses that passed on the phone: straight, left, right, smile.</summary>
    public List<string> LivenessChallenges { get; private set; } = new();
    public int LivenessAttempts { get; private set; }

    // ── ID photo checks from the phone (OCR + face on the card) ──
    public bool? IdTextFound { get; private set; }
    public bool? IdNumberFound { get; private set; }
    public bool? IdNumberMatches { get; private set; }
    public bool? IdFaceFound { get; private set; }

    /// <summary>When the selfie and pose selfies were deleted under the retention rule. The ID image is kept.</summary>
    public DateTimeOffset? SelfieImagesDeletedAtUtc { get; private set; }

    public const int MaxConsentVersionLength = 40;
    public static readonly string[] LivenessMethods = { "on-device", "manual" };
    public static readonly string[] AllowedPoses = { "left", "right", "up", "smile" };
    public static readonly string[] AllowedChallenges = { "straight", "left", "right", "up", "smile" };

    /// <summary>Selfies of an approved artisan are deleted this long after approval.</summary>
    public static readonly TimeSpan VerifiedSelfieRetention = TimeSpan.FromHours(24);
    /// <summary>Selfies of a declined application are kept this long (they may send a fix back).</summary>
    public static readonly TimeSpan RejectedSelfieRetention = TimeSpan.FromDays(30);

    /// <summary>Records the consent the artisan gave for this submission. Required on every submit and resubmit.</summary>
    public void RecordConsent(string? consentVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(consentVersion))
            throw new ArgumentException("Agree to the consent before sending.", nameof(consentVersion));
        var v = consentVersion.Trim();
        ConsentVersion = v.Length > MaxConsentVersionLength ? v[..MaxConsentVersionLength] : v;
        ConsentedAtUtc = now;
    }

    /// <summary>Records what the phone's liveness check reported. Null means an older app that ran no check.</summary>
    public void RecordLiveness(string? method, bool? passed, IEnumerable<string>? challenges, int attempts)
    {
        if (method is null)
        {
            LivenessMethod = null;
            LivenessPassed = null;
            LivenessChallenges = new();
            LivenessAttempts = 0;
            return;
        }
        var m = method.Trim().ToLowerInvariant();
        if (!LivenessMethods.Contains(m))
            throw new ArgumentException($"Unknown liveness method '{method}'.", nameof(method));
        LivenessMethod = m;
        LivenessPassed = passed;
        LivenessChallenges = (challenges ?? Array.Empty<string>())
            .Select(c => (c ?? "").Trim().ToLowerInvariant())
            .Where(c => AllowedChallenges.Contains(c))
            .Distinct()
            .ToList();
        LivenessAttempts = Math.Clamp(attempts, 0, 1000);
    }

    /// <summary>Records the ID photo signals the phone reported (all optional).</summary>
    public void RecordIdChecks(bool? textFound, bool? numberFound, bool? numberMatches, bool? faceFound)
    {
        IdTextFound = textFound;
        IdNumberFound = numberFound;
        IdNumberMatches = numberMatches;
        IdFaceFound = faceFound;
    }

    /// <summary>True when this application's selfies are due for deletion under the retention rule.</summary>
    public bool IsSelfieDeletionDue(DateTimeOffset now) =>
        IsSelfieDeletionDue(Status, ReviewedAtUtc, SelfieImagesDeletedAtUtc, now);

    /// <summary>Pure retention rule: Verified → 24h after review, Rejected → 30 days after review, Pending never.</summary>
    public static bool IsSelfieDeletionDue(
        ArtisanVerificationStatus status, DateTimeOffset? reviewedAtUtc, DateTimeOffset? deletedAtUtc, DateTimeOffset now)
    {
        if (deletedAtUtc is not null || reviewedAtUtc is null) return false;
        return status switch
        {
            ArtisanVerificationStatus.Verified => now - reviewedAtUtc.Value >= VerifiedSelfieRetention,
            ArtisanVerificationStatus.Rejected => now - reviewedAtUtc.Value >= RejectedSelfieRetention,
            _ => false,
        };
    }

    /// <summary>The selfie files were deleted: clear their keys and stamp the time. The ID document stays.</summary>
    public void MarkSelfieImagesDeleted(DateTimeOffset now)
    {
        SelfieKey = string.Empty;
        PoseSelfiesJson = null;
        SelfieImagesDeletedAtUtc = now;
    }

    /// <summary>Review state (Pending → Verified/Rejected), same vocabulary as the profile.</summary>
    public ArtisanVerificationStatus Status { get; private set; }

    public string? ReviewNote { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    // ── Changes requested (the application stays Pending, one check reopens) ──
    /// <summary>The check the reviewer asked the artisan to fix, or null when nothing is open.</summary>
    public VerificationCheck? OpenCheck { get; private set; }
    public string? OpenReasonCode { get; private set; }
    public string? OpenNote { get; private set; }
    /// <summary>When the artisan last sent a fix back; resubmissions go to the front of the queue.</summary>
    public DateTimeOffset? ResubmittedAtUtc { get; private set; }
    public int ResubmissionCount { get; private set; }

    public bool HasOpenCheck => OpenCheck is not null;

    /// <summary>Reviewer asks for one thing to be fixed. The application stays Pending; only that check reopens.</summary>
    public void RequestChanges(VerificationCheck check, string? reasonCode, string note, DateTimeOffset now)
    {
        if (Status == ArtisanVerificationStatus.Verified)
            throw new InvalidOperationException("An approved application cannot have changes requested.");
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Say what needs to change.", nameof(note));
        Status = ArtisanVerificationStatus.Pending;
        OpenCheck = check;
        OpenReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim();
        OpenNote = note.Trim();
        ReviewNote = note.Trim();
        ReviewedAtUtc = now;
    }

    /// <summary>The artisan sent the fix back: the open check closes and the application returns to the queue, at the front.</summary>
    public void MarkResubmitted(DateTimeOffset now)
    {
        OpenCheck = null;
        OpenReasonCode = null;
        OpenNote = null;
        ReviewNote = null;
        ReviewedAtUtc = null;
        Status = ArtisanVerificationStatus.Pending;
        ResubmittedAtUtc = now;
        ResubmissionCount += 1;
    }

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
        SelfieImagesDeletedAtUtc = null;
        Status = ArtisanVerificationStatus.Pending;
        ReviewNote = null;
        ReviewedAtUtc = null;
        // A fresh identity upload counts as the fix for an open identity/selfie check.
        OpenCheck = null;
        OpenReasonCode = null;
        OpenNote = null;
        ResubmittedAtUtc = now;
        ResubmissionCount += 1;
    }

    public void Approve(DateTimeOffset now)
    {
        Status = ArtisanVerificationStatus.Verified;
        ReviewedAtUtc = now;
        ReviewNote = null;
        OpenCheck = null;
        OpenReasonCode = null;
        OpenNote = null;
    }

    /// <summary>Decline. The failed check stays open so the artisan fixes only that one and
    /// sends it back (a fresh ID upload for identity/selfie, Send it back for the rest).</summary>
    public void Reject(string? reason, DateTimeOffset now, VerificationCheck? failedCheck = null, string? reasonCode = null)
    {
        Status = ArtisanVerificationStatus.Rejected;
        ReviewedAtUtc = now;
        ReviewNote = reason;
        OpenCheck = failedCheck;
        OpenReasonCode = failedCheck is null || string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim();
        OpenNote = failedCheck is null || string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>Older declines did not keep the check open; reopen it from the history.</summary>
    public void ReopenCheck(VerificationCheck check)
    {
        if (Status != ArtisanVerificationStatus.Rejected || OpenCheck is not null) return;
        OpenCheck = check;
        OpenNote = ReviewNote;
    }
}
