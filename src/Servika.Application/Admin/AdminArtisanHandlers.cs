using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Domain.Payments;

namespace Servika.Application.Admin;

/// <summary>
/// The admin artisan directory — every artisan (any verification status) with their
/// reputation, verification, and standing. Standing (unpaid cash-job commission +
/// whether it restricts them) is computed from the append-only ledger against the
/// admin-set debt limit, in two batched queries — no per-artisan N+1.
/// </summary>
public sealed class ListAdminArtisansHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IUserRepository _users;
    private readonly IWalletRepository _wallet;
    private readonly IPlatformSettingsRepository _settings;

    public ListAdminArtisansHandler(
        ICatalogueRepository catalogue,
        IUserRepository users,
        IWalletRepository wallet,
        IPlatformSettingsRepository settings)
    {
        _catalogue = catalogue;
        _users = users;
        _wallet = wallet;
        _settings = settings;
    }

    public async Task<IReadOnlyList<AdminArtisanDto>> HandleAsync(CancellationToken ct)
    {
        var artisans = await _catalogue.ListAllArtisansAsync(ct);
        var emails = (await _users.ListAsync(null, ct)).ToDictionary(u => u.Id, u => u.Email);
        var balances = await _wallet.ListBalancesAsync(WalletOwnerType.Artisan, ct);
        var maxDebt = (await _settings.GetOrCreateAsync(ct)).MaxCommissionDebtNaira;

        return artisans.Select(a =>
        {
            var balance = balances.TryGetValue(a.Id, out var b) ? b : 0;
            var owed = Math.Max(0, -balance);
            return new AdminArtisanDto(
                a.Id,
                a.UserId,
                a.FullName,
                a.UserId is { } uid && emails.TryGetValue(uid, out var e) ? e : "",
                a.Specialty,
                a.Rating,
                a.ReviewCount,
                a.IsAvailable,
                a.VerificationStatus.ToString(),
                !string.IsNullOrEmpty(a.CertificateKey),
                a.CategorySlugs,
                string.IsNullOrEmpty(a.PhotoKey) ? null : $"/api/v1/artisans/{a.Id}/photo",
                a.GalleryPhotoKeys.Count,
                owed,
                balance < -maxDebt);
        }).ToList();
    }
}

/// <summary>Serves an artisan's uploaded work certificate to the admin as a base64
/// data URI (the certificate isn't public; only admins vet it). 404 unknown artisan;
/// null data URI when the artisan uploaded no certificate.</summary>
public sealed class GetAdminArtisanCertificateHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IFileStorage _storage;

    public GetAdminArtisanCertificateHandler(ICatalogueRepository catalogue, IFileStorage storage)
    {
        _catalogue = catalogue;
        _storage = storage;
    }

    public async Task<AdminArtisanCertificateDto> HandleAsync(Guid artisanId, CancellationToken ct)
    {
        var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
            ?? throw new NotFoundException("Artisan was not found.");

        if (string.IsNullOrEmpty(artisan.CertificateKey))
            return new AdminArtisanCertificateDto(null);

        var file = await _storage.GetAsync(artisan.CertificateKey, ct);
        return new AdminArtisanCertificateDto(
            file is null ? null : $"data:{file.ContentType};base64,{Convert.ToBase64String(file.Content)}");
    }
}
