using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// The signed-in artisan requests a payout of their earnings to a bank account.
/// Resolves the artisan to their profile (the ledger owner) and delegates to the
/// shared <see cref="WithdrawalService"/>, which validates against the
/// ledger-computed balance and disburses.
/// </summary>
public sealed class RequestWithdrawalHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IPlatformSettingsRepository _settings;
    private readonly WithdrawalService _service;

    public RequestWithdrawalHandler(
        ICatalogueRepository catalogue,
        IPlatformSettingsRepository settings,
        WithdrawalService service)
    {
        _catalogue = catalogue;
        _settings = settings;
        _service = service;
    }

    public async Task<WithdrawalDto> HandleAsync(
        Guid artisanUserId, RequestWithdrawalRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var settings = await _settings.GetOrCreateAsync(ct);
        return await _service.WithdrawAsync(
            WalletOwnerType.Artisan, profile.Id, artisanUserId,
            request, settings.MinWithdrawalNaira, ct);
    }
}
