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
    private readonly IClock _clock;

    public SubmitKycHandler(
        IArtisanKycRepository kyc,
        ICatalogueRepository catalogue,
        IFileStorage storage,
        IKycVerificationProvider provider,
        IClock clock)
    {
        _kyc = kyc;
        _catalogue = catalogue;
        _storage = storage;
        _provider = provider;
        _clock = clock;
    }

    public async Task<KycStatusDto> HandleAsync(
        Guid artisanUserId, SubmitKycRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<KycIdType>(request.IdType, ignoreCase: true, out var idType))
            throw new ArgumentException($"Unknown ID type '{request.IdType}'.");

        var selfie = DecodeImage(request.SelfieBase64, "selfie");
        var idImage = DecodeImage(request.IdImageBase64, "ID document");

        var now = _clock.UtcNow;
        var selfieKey = await _storage.SaveAsync(selfie, "image/jpeg", ct);
        var idKey = await _storage.SaveAsync(idImage, "image/jpeg", ct);

        var existing = await _kyc.GetForUserAsync(artisanUserId, ct);
        ArtisanKyc submission;
        if (existing is not null)
        {
            existing.Resubmit(idType, request.IdNumber, selfieKey, idKey, now);
            submission = existing;
        }
        else
        {
            submission = ArtisanKyc.Submit(artisanUserId, idType, request.IdNumber, selfieKey, idKey, now);
            _kyc.Add(submission);
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
