using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Abstractions.Verification;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The signed-in artisan submits KYC (selfie + one government ID). Images are
/// decoded and stored via <see cref="IFileStorage"/>; the submission is then run
/// through the <see cref="IKycVerificationProvider"/> (manual → stays Pending;
/// dev auto-approve / automated → decided now). On approval the artisan's profile
/// is marked Verified so it appears in the catalogue and can be booked.
/// </summary>
public sealed class SubmitKycHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _storage;
    private readonly IKycVerificationProvider _provider;
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IVerificationEventRepository _events;
    private readonly IClock _clock;

    public SubmitKycHandler(
        IArtisanKycRepository kyc,
        ICatalogueRepository catalogue,
        IFileStorage storage,
        IKycVerificationProvider provider,
        IClock clock,
        IArtisanGuarantorRepository guarantors,
        IPlatformSettingsRepository settings,
        IVerificationEventRepository events)
    {
        _events = events;
        _kyc = kyc;
        _catalogue = catalogue;
        _storage = storage;
        _provider = provider;
        _guarantors = guarantors;
        _settings = settings;
        _clock = clock;
    }

    public async Task<KycStatusDto> HandleAsync(
        Guid artisanUserId, SubmitKycRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<KycIdType>(request.IdType, ignoreCase: true, out var idType))
            throw new ArgumentException($"Unknown ID type '{request.IdType}'.");

        // Guarantors are required unless the admin switched the rule off or waived it for this artisan.
        var settings = await _settings.GetOrCreateAsync(ct);
        var profileForPolicy = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (settings.RequireGuarantors && !(profileForPolicy?.GuarantorsWaived ?? false))
        {
            var have = await _guarantors.CountForUserAsync(artisanUserId, ct);
            if (have < settings.RequiredGuarantorCount)
                throw new ConflictException(
                    $"Add {settings.RequiredGuarantorCount} guarantor{(settings.RequiredGuarantorCount == 1 ? "" : "s")} before sending your application. You have {have}.");
        }

        var selfie = DecodeImage(request.SelfieBase64, "selfie");
        var idImage = DecodeImage(request.IdImageBase64, "ID document");

        var now = _clock.UtcNow;
        var selfieKey = await _storage.SaveAsync(selfie, "image/jpeg", ct);
        var idKey = await _storage.SaveAsync(idImage, "image/jpeg", ct);

        // Pose selfies (left / right): decoded and stored beside the straight one.
        var poseShots = new List<PoseShot>();
        foreach (var p in (request.PoseSelfies ?? Array.Empty<PoseSelfieRequest>()).Take(3))
        {
            var pose = (p.Pose ?? "").Trim().ToLowerInvariant();
            if (pose is not ("left" or "right" or "up")) throw new ArgumentException($"Unknown pose '{p.Pose}'.");
            var bytes = DecodeImage(p.ImageBase64, $"{pose} pose selfie");
            poseShots.Add(new PoseShot(pose, await _storage.SaveAsync(bytes, "image/jpeg", ct)));
        }
        var poseJson = poseShots.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(poseShots);

        var existing = await _kyc.GetForUserAsync(artisanUserId, ct);
        ArtisanKyc submission;
        if (existing is not null)
        {
            var wasOpen = existing.OpenCheck;
            existing.Resubmit(idType, request.IdNumber, selfieKey, idKey, now);
            existing.SetPoseSelfies(poseJson);
            submission = existing;
            _events.Add(VerificationEvent.Create(submission.Id, artisanUserId, null, VerificationEventAction.Resubmitted,
                wasOpen ?? VerificationCheck.Identity, null, null, now));
        }
        else
        {
            submission = ArtisanKyc.Submit(artisanUserId, idType, request.IdNumber, selfieKey, idKey, now);
            submission.SetPoseSelfies(poseJson);
            _kyc.Add(submission);
            _events.Add(VerificationEvent.Create(submission.Id, artisanUserId, null, VerificationEventAction.Submitted, null, null, null, now));
        }

        // Decide (manual → Pending; dev auto-approve / automated → resolved).
        var decision = await _provider.ReviewAsync(submission, ct);
        if (decision == ArtisanVerificationStatus.Verified)
            submission.Approve(now);
        else if (decision == ArtisanVerificationStatus.Rejected)
            submission.Reject("Automated check failed.", now);

        // Keep the artisan's profile in sync with the decision (if they've onboarded).
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct);
        if (profile is not null)
        {
            if (submission.Status == ArtisanVerificationStatus.Verified) profile.MarkVerified();
            else if (submission.Status == ArtisanVerificationStatus.Rejected) profile.MarkRejected();
        }

        await _kyc.SaveChangesAsync(ct);
        return submission.ToStatusDto();
    }

    private static byte[] DecodeImage(string? base64, string label)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new ArgumentException($"A {label} image is required.");

        // Accept a raw base64 string or a data: URI ("data:image/jpeg;base64,....").
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0 ? base64[(comma + 1)..] : base64;
        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new ArgumentException($"The {label} image is not valid base64.");
        }
    }
}

/// <summary>Stored shape of one pose selfie: which pose the app asked for and the storage key.</summary>
public sealed record PoseShot(string Pose, string Key);
