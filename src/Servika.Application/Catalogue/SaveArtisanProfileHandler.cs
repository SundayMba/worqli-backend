using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

// (KYC repo injected so a profile created after KYC approval is already Verified.)

/// <summary>
/// Creates or updates the signed-in artisan's marketplace profile (self-onboarding
/// + service setup). Upsert: no profile yet → create (linked to the account, name
/// taken from the account, auto-verified for now); otherwise update the editable
/// fields. Every supplied category slug must exist.
/// </summary>
public sealed class SaveArtisanProfileHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly IArtisanKycRepository _kyc;
    private readonly IFileStorage _files;

    public SaveArtisanProfileHandler(
        ICatalogueRepository catalogue, IUserRepository users, IArtisanKycRepository kyc,
        IFileStorage files)
    {
        _catalogue = catalogue;
        _users = users;
        _kyc = kyc;
        _files = files;
    }

    public async Task<MyArtisanProfileDto> HandleAsync(
        Guid artisanUserId, SaveArtisanProfileRequest request, CancellationToken ct)
    {
        var slugs = (request.CategorySlugs ?? new())
            .Select(s => s?.Trim() ?? string.Empty)
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();
        if (slugs.Count == 0)
            throw new ArgumentException("Select at least one service category.");
        foreach (var slug in slugs)
        {
            if (!await _catalogue.CategoryExistsAsync(slug, ct))
                throw new NotFoundException($"Category '{slug}' was not found.");
        }

        var services = (request.Services ?? new())
            .Select(s => s?.Trim() ?? string.Empty)
            .Where(s => s.Length > 0)
            .ToList();

        // Store the uploaded photos (if any) before touching the row, so a
        // bad image fails the request without a half-updated profile.
        var photoKey = await StorePhotoAsync(request.PhotoBase64, "profile photo", ct);
        var coverKey = await StorePhotoAsync(request.CoverPhotoBase64, "cover photo", ct);
        var certificateKey = await StorePhotoAsync(request.CertificateBase64, "certificate", ct);

        var existing = await _catalogue.GetArtisanByUserIdForUpdateAsync(artisanUserId, ct);
        if (existing is not null)
        {
            existing.UpdateDetails(
                request.Specialty, slugs, services, request.About,
                request.ExperienceYears, request.Location, request.InspectionFeeNaira,
                request.Latitude, request.Longitude);
            if (photoKey is not null) existing.SetPhoto(photoKey);
            if (coverKey is not null) existing.SetCoverPhoto(coverKey);
            if (certificateKey is not null) existing.SetCertificate(certificateKey);
            await _catalogue.SaveChangesAsync(ct);
            return existing.ToMyProfileDto();
        }

        var user = await _users.FindByIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Account not found.");

        var profile = ArtisanProfile.CreateForUser(
            userId: artisanUserId,
            fullName: user.FullName,
            specialty: request.Specialty,
            categorySlugs: slugs,
            services: services,
            about: request.About,
            experienceYears: request.ExperienceYears,
            location: request.Location,
            inspectionFeeNaira: request.InspectionFeeNaira,
            latitude: request.Latitude,
            longitude: request.Longitude,
            imageKey: request.ImageKey ?? string.Empty);

        if (photoKey is not null) profile.SetPhoto(photoKey);
        if (coverKey is not null) profile.SetCoverPhoto(coverKey);
        if (certificateKey is not null) profile.SetCertificate(certificateKey);

        // If they already passed KYC before creating the profile, reflect it now.
        var kyc = await _kyc.GetForUserAsync(artisanUserId, ct);
        if (kyc?.Status == ArtisanVerificationStatus.Verified)
            profile.MarkVerified();

        _catalogue.AddArtisan(profile);
        await _catalogue.SaveChangesAsync(ct);
        return profile.ToMyProfileDto();
    }

    /// <summary>Decodes and stores an optional uploaded image, returning its
    /// storage key — or null when none was sent. Accepts raw base64 or a
    /// data: URI, same as the KYC upload.</summary>
    private async Task<string?> StorePhotoAsync(string? base64, string label, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;

        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0
            ? base64[(comma + 1)..]
            : base64;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw new ArgumentException($"The {label} is not valid base64.");
        }
        if (bytes.Length == 0)
            throw new ArgumentException($"The {label} is empty.");

        return await _files.SaveAsync(bytes, "image/jpeg", ct);
    }
}
