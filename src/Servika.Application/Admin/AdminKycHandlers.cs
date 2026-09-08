using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Domain.Catalogue;

namespace Servika.Application.Admin;

/// <summary>
/// Admin KYC verification — the "document onboarding verification" queue. Lists
/// submissions, serves the selfie + ID images for a human to eyeball, and applies
/// the decision, which flips both the KYC row and the artisan's profile status
/// (Verified profiles appear in the catalogue and can be booked).
/// </summary>
public sealed class ListKycSubmissionsHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly IUserRepository _users;

    public ListKycSubmissionsHandler(IArtisanKycRepository kyc, IUserRepository users)
    {
        _kyc = kyc;
        _users = users;
    }

    public async Task<IReadOnlyList<KycSubmissionDto>> HandleAsync(string? status, CancellationToken ct)
    {
        ArtisanVerificationStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ArtisanVerificationStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"'{status}' is not a valid KYC status.");
            filter = parsed;
        }

        var submissions = await _kyc.ListAsync(filter, ct);
        var list = new List<KycSubmissionDto>(submissions.Count);
        foreach (var k in submissions)
        {
            var user = await _users.FindByIdAsync(k.UserId, ct);
            list.Add(new KycSubmissionDto(
                k.Id, k.UserId, user?.FullName ?? "Artisan", user?.Email ?? "",
                k.IdType.ToString(), k.IdNumber, k.Status.ToString(),
                k.SubmittedAtUtc, k.ReviewedAtUtc, k.ReviewNote));
        }
        return list;
    }
}

/// <summary>A single submission with its document images inlined for review.</summary>
public sealed class GetKycSubmissionHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly IUserRepository _users;
    private readonly IFileStorage _storage;
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly ICatalogueRepository _catalogue;

    public GetKycSubmissionHandler(
        IArtisanKycRepository kyc, IUserRepository users, IFileStorage storage,
        IArtisanGuarantorRepository guarantors, ICatalogueRepository catalogue)
    {
        _kyc = kyc;
        _users = users;
        _storage = storage;
        _guarantors = guarantors;
        _catalogue = catalogue;
    }

    public async Task<KycSubmissionDetailDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var k = await _kyc.GetByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"KYC submission '{id}' was not found.");

        var user = await _users.FindByIdAsync(k.UserId, ct);
        // Everything the applicant handed over, on one screen: identity images,
        // the people who vouch for them, and where the money would land.
        var profile = await _catalogue.GetArtisanByUserIdAsync(k.UserId, ct);
        var guarantors = new List<AdminGuarantorDto>();
        foreach (var g in await _guarantors.ListForUserAsync(k.UserId, ct))
        {
            guarantors.Add(new AdminGuarantorDto(
                g.Id, g.FullName, g.Phone, g.Relationship, g.YearsKnown, g.Occupation, g.Address,
                g.IdPhotoKey is null ? null : await DataUriAsync(g.IdPhotoKey, ct)));
        }

        return new KycSubmissionDetailDto(
            k.Id, k.UserId, user?.FullName ?? "Artisan", user?.Email ?? "",
            k.IdType.ToString(), k.IdNumber, k.Status.ToString(),
            k.SubmittedAtUtc, k.ReviewedAtUtc, k.ReviewNote,
            await DataUriAsync(k.SelfieKey, ct),
            await DataUriAsync(k.IdDocumentKey, ct),
            guarantors,
            profile?.PayoutBankName,
            profile?.PayoutAccountNumber is { Length: >= 4 } acct ? $"••••{acct[^4..]}" : null,
            profile?.PayoutAccountName,
            profile?.Specialty,
            profile?.Location,
            profile is null || string.IsNullOrEmpty(profile.PhotoKey) ? null : $"/api/v1/artisans/{profile.Id}/photo",
            profile?.HasCertificate ?? false);
    }

    private async Task<string?> DataUriAsync(string key, CancellationToken ct)
    {
        var file = await _storage.GetAsync(key, ct);
        return file is null ? null : $"data:{file.ContentType};base64,{Convert.ToBase64String(file.Content)}";
    }
}

/// <summary>Applies an admin decision (approve/reject) to a KYC submission and
/// syncs the artisan's profile status.</summary>
public sealed class ReviewKycHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;

    public ReviewKycHandler(
        IArtisanKycRepository kyc, ICatalogueRepository catalogue, IClock clock)
    {
        _kyc = kyc;
        _catalogue = catalogue;
        _clock = clock;
    }

    public async Task<KycSubmissionDto> HandleAsync(
        Guid id, bool approve, string? reason, CancellationToken ct)
    {
        var submission = await _kyc.GetByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"KYC submission '{id}' was not found.");

        var now = _clock.UtcNow;
        if (approve) submission.Approve(now);
        else submission.Reject(reason, now);

        // Keep the artisan's profile in sync (they may not have onboarded a profile).
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(submission.UserId, ct);
        if (profile is not null)
        {
            if (approve) profile.MarkVerified();
            else profile.MarkRejected();
        }

        await _kyc.SaveChangesAsync(ct);

        return new KycSubmissionDto(
            submission.Id, submission.UserId, string.Empty, string.Empty,
            submission.IdType.ToString(), submission.IdNumber, submission.Status.ToString(),
            submission.SubmittedAtUtc, submission.ReviewedAtUtc, submission.ReviewNote);
    }
}
