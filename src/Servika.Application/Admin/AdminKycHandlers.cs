using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Catalogue;
using Servika.Application.Common;
using Servika.Application.Notifications;
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
                k.SubmittedAtUtc, k.ReviewedAtUtc, k.ReviewNote,
                k.OpenCheck?.ToString(), k.ResubmittedAtUtc, k.ResubmissionCount));
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
    private readonly IPlatformSettingsRepository _settings;
    private readonly IVerificationEventRepository _events;

    public GetKycSubmissionHandler(
        IArtisanKycRepository kyc, IUserRepository users, IFileStorage storage,
        IArtisanGuarantorRepository guarantors, ICatalogueRepository catalogue,
        IPlatformSettingsRepository settings,
        IVerificationEventRepository events)
    {
        _events = events;
        _kyc = kyc;
        _users = users;
        _storage = storage;
        _guarantors = guarantors;
        _settings = settings;
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
        var settings = await _settings.GetOrCreateAsync(ct);
        var mine = await _guarantors.ListForUserAsync(k.UserId, ct);
        var warnings = await GuarantorWarningsAsync(user, mine, ct);
        var guarantors = new List<AdminGuarantorDto>();
        foreach (var g in mine)
        {
            var caller = g.ContactedByUserId is { } cb ? (await _users.FindByIdAsync(cb, ct))?.FullName : null;
            guarantors.Add(new AdminGuarantorDto(
                g.Id, g.FullName, g.Phone, g.Relationship, g.YearsKnown, g.Occupation, g.Address,
                g.IdPhotoKey is null ? null : await DataUriAsync(g.IdPhotoKey, ct),
                g.ContactStatus.ToString(), g.ContactNote, g.ContactedAtUtc, caller,
                warnings.TryGetValue(g.Id, out var w) ? w : Array.Empty<string>()));
        }

        return new KycSubmissionDetailDto(
            k.Id, k.UserId, user?.FullName ?? "Artisan", user?.Email ?? "",
            k.IdType.ToString(), k.IdNumber, k.Status.ToString(),
            k.SubmittedAtUtc, k.ReviewedAtUtc, k.ReviewNote,
            await DataUriAsync(k.SelfieKey, ct),
            await DataUriAsync(k.IdDocumentKey, ct),
            guarantors,
            profile?.PayoutBankName,
            string.IsNullOrWhiteSpace(profile?.PayoutAccountNumber) ? null : profile.PayoutAccountNumber,
            profile?.PayoutAccountName,
            profile?.Specialty,
            profile?.Location,
            profile is null || string.IsNullOrEmpty(profile.PhotoKey) ? null : $"/api/v1/artisans/{profile.Id}/photo",
            profile?.HasCertificate ?? false,
            profile?.Id,
            settings.RequireGuarantors && !(profile?.GuarantorsWaived ?? false),
            profile?.GuarantorsWaived ?? false,
            settings.RequiredGuarantorCount,
            profile?.NinLookupStatus,
            profile?.NinLookupName,
            profile?.NinLookupCheckedAtUtc,
            k.OpenCheck?.ToString(),
            k.OpenReasonCode,
            k.OpenNote,
            k.ResubmittedAtUtc,
            k.ResubmissionCount,
            await (await _events.ListForKycAsync(k.Id, ct)).ToAdminDtosAsync(_users, user?.FullName ?? "Artisan", ct),
            await PoseSelfiesAsync(k.PoseSelfiesJson, ct));
    }

    private async Task<IReadOnlyList<AdminPoseSelfieDto>> PoseSelfiesAsync(string? json, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AdminPoseSelfieDto>();
        List<Catalogue.PoseShot>? shots;
        try { shots = System.Text.Json.JsonSerializer.Deserialize<List<Catalogue.PoseShot>>(json); }
        catch (System.Text.Json.JsonException) { return Array.Empty<AdminPoseSelfieDto>(); }
        var list = new List<AdminPoseSelfieDto>();
        foreach (var s in shots ?? new()) list.Add(new AdminPoseSelfieDto(s.Pose, await DataUriAsync(s.Key, ct)));
        return list;
    }

    /// <summary>Phone-number red flags per guarantor: the artisan's own number, the same
    /// number twice, or a number other artisans also listed (how a ring of fake
    /// accounts shows up). Names other artisans so the reviewer can look them up.</summary>
    private async Task<Dictionary<Guid, string[]>> GuarantorWarningsAsync(
        Domain.Users.User? artisan, IReadOnlyList<ArtisanGuarantor> mine, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string[]>();
        if (mine.Count == 0) return result;
        var own = ArtisanGuarantor.NormalizePhone(artisan?.PhoneNumber);
        var all = await _guarantors.ListAllPhonesAsync(ct);
        var otherOwners = all
            .Where(x => x.ArtisanUserId != (artisan?.Id ?? Guid.Empty))
            .GroupBy(x => ArtisanGuarantor.NormalizePhone(x.Phone))
            .ToDictionary(gr => gr.Key, gr => gr.Select(x => x.ArtisanUserId).Distinct().ToList());

        foreach (var g in mine)
        {
            var phone = ArtisanGuarantor.NormalizePhone(g.Phone);
            var list = new List<string>();
            if (own.Length >= 10 && phone == own) list.Add("This is the artisan's own phone number.");
            if (mine.Count(x => x.Id != g.Id && ArtisanGuarantor.NormalizePhone(x.Phone) == phone) > 0)
                list.Add("Same number as their other guarantor.");
            if (otherOwners.TryGetValue(phone, out var others) && others.Count > 0)
            {
                var names = new List<string>();
                foreach (var id in others.Take(3))
                    names.Add((await _users.FindByIdAsync(id, ct))?.FullName ?? "an artisan");
                var more = others.Count > 3 ? $" and {others.Count - 3} more" : "";
                list.Add($"Also listed as a guarantor by {others.Count} other artisan{(others.Count == 1 ? "" : "s")}: {string.Join(", ", names)}{more}.");
            }
            if (list.Count > 0) result[g.Id] = list.ToArray();
        }
        return result;
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
    private readonly NotificationEmitter _notifications;
    private readonly IVerificationEventRepository _events;
    private readonly IClock _clock;

    public ReviewKycHandler(
        IArtisanKycRepository kyc, ICatalogueRepository catalogue, NotificationEmitter notifications,
        IVerificationEventRepository events, IClock clock)
    {
        _kyc = kyc;
        _catalogue = catalogue;
        _notifications = notifications;
        _events = events;
        _clock = clock;
    }

    public async Task<KycSubmissionDto> HandleAsync(
        Guid adminUserId, Guid id, bool approve, string? reason, string? check, string? reasonCode, CancellationToken ct)
    {
        var submission = await _kyc.GetByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"KYC submission '{id}' was not found.");

        var now = _clock.UtcNow;
        VerificationCheck? failed = string.IsNullOrWhiteSpace(check) ? null : VerificationEventMapping.ParseCheck(check);
        if (approve) submission.Approve(now);
        else submission.Reject(reason, now);
        _events.Add(VerificationEvent.Create(submission.Id, submission.UserId, adminUserId,
            approve ? VerificationEventAction.Approved : VerificationEventAction.Declined, failed, reasonCode, approve ? null : reason, now));

        // Keep the artisan's profile in sync (they may not have onboarded a profile).
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(submission.UserId, ct);
        if (profile is not null)
        {
            if (approve) profile.MarkVerified();
            else profile.MarkRejected();
        }

        // In-app + push, in the same transaction as the decision.
        _notifications.KycReviewed(submission.UserId, approve, reason);

        await _kyc.SaveChangesAsync(ct);

        return new KycSubmissionDto(
            submission.Id, submission.UserId, string.Empty, string.Empty,
            submission.IdType.ToString(), submission.IdNumber, submission.Status.ToString(),
            submission.SubmittedAtUtc, submission.ReviewedAtUtc, submission.ReviewNote,
            submission.OpenCheck?.ToString(), submission.ResubmittedAtUtc, submission.ResubmissionCount);
    }
}

/// <summary>
/// The third outcome: ask the artisan to fix one check. The application stays Pending,
/// the artisan keeps every other check, and the note is what they read word for word.
/// </summary>
public sealed class RequestKycChangesHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly NotificationEmitter _notifications;
    private readonly IVerificationEventRepository _events;
    private readonly IClock _clock;

    public RequestKycChangesHandler(IArtisanKycRepository kyc, NotificationEmitter notifications, IVerificationEventRepository events, IClock clock)
    {
        _kyc = kyc;
        _notifications = notifications;
        _events = events;
        _clock = clock;
    }

    public async Task<KycSubmissionDto> HandleAsync(Guid adminUserId, Guid id, RequestChangesRequest request, CancellationToken ct)
    {
        var submission = await _kyc.GetByIdForUpdateAsync(id, ct)
            ?? throw new NotFoundException($"KYC submission '{id}' was not found.");
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length < 10)
            throw new ArgumentException("Write at least ten characters so the artisan knows what to fix.");
        var check = VerificationEventMapping.ParseCheck(request.Check);

        var now = _clock.UtcNow;
        try
        {
            submission.RequestChanges(check, request.ReasonCode, request.Note, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message);
        }
        _events.Add(VerificationEvent.Create(submission.Id, submission.UserId, adminUserId,
            VerificationEventAction.ChangesRequested, check, request.ReasonCode, request.Note, now));
        _notifications.KycChangesRequested(submission.UserId, check.ToString(), request.Note.Trim());

        await _kyc.SaveChangesAsync(ct);
        return new KycSubmissionDto(
            submission.Id, submission.UserId, string.Empty, string.Empty,
            submission.IdType.ToString(), submission.IdNumber, submission.Status.ToString(),
            submission.SubmittedAtUtc, submission.ReviewedAtUtc, submission.ReviewNote,
            submission.OpenCheck?.ToString(), submission.ResubmittedAtUtc, submission.ResubmissionCount);
    }
}

/// <summary>The reviewer called a guarantor and records the outcome on the applicant file.
/// Admin-only; never shown to the artisan.</summary>
public sealed class RecordGuarantorContactHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly IArtisanGuarantorRepository _guarantors;
    private readonly Abstractions.Time.IClock _clock;

    public RecordGuarantorContactHandler(IArtisanKycRepository kyc, IArtisanGuarantorRepository guarantors, Abstractions.Time.IClock clock)
    {
        _kyc = kyc;
        _guarantors = guarantors;
        _clock = clock;
    }

    public async Task HandleAsync(Guid adminUserId, Guid kycId, Guid guarantorId, RecordGuarantorContactRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<GuarantorContactStatus>(request.Status, ignoreCase: true, out var status))
            throw new ArgumentException("Status must be NotCalled, Confirmed, Unreachable or Denied.");
        var k = await _kyc.GetByIdForUpdateAsync(kycId, ct)
            ?? throw new NotFoundException($"KYC submission '{kycId}' was not found.");
        var g = await _guarantors.FindByIdForUpdateAsync(guarantorId, ct);
        if (g is null || g.ArtisanUserId != k.UserId)
            throw new NotFoundException("That guarantor is not on this application.");
        g.RecordContact(status, request.Note, adminUserId, _clock.UtcNow);
        await _guarantors.SaveChangesAsync(ct);
    }
}
