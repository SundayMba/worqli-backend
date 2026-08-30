using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Starts an artisan's payment of their outstanding cash-job service fees — the
/// "Settle balance" button. The amount is whatever the ledger says is owed
/// (never the client), the gateway hosts the checkout, and the webhook credits
/// the ledger on success — which lifts any restriction instantly, since
/// standing is always computed live from the ledger.
/// </summary>
public sealed class SettleCommissionHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IWalletRepository _wallet;
    private readonly IPaymentRepository _payments;
    private readonly IUserRepository _users;
    private readonly IPaymentGateway _gateway;
    private readonly IClock _clock;

    public SettleCommissionHandler(
        ICatalogueRepository catalogue,
        IWalletRepository wallet,
        IPaymentRepository payments,
        IUserRepository users,
        IPaymentGateway gateway,
        IClock clock)
    {
        _catalogue = catalogue;
        _wallet = wallet;
        _payments = payments;
        _users = users;
        _gateway = gateway;
        _clock = clock;
    }

    public async Task<PaymentInitResponse> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("No artisan profile is linked to this account.");

        var balance = await _wallet.GetBalanceAsync(WalletOwnerType.Artisan, profile.Id, ct);
        var owed = Math.Max(0, -balance);
        if (owed <= 0)
            throw new ConflictException("You have no outstanding service fees to settle.");

        var user = await _users.FindByIdAsync(artisanUserId, ct);
        var email = string.IsNullOrWhiteSpace(user?.Email) ? "artisan@servika.app" : user!.Email;

        var reference = $"svk_fee_{Guid.NewGuid():N}";
        var result = await _gateway.InitializeAsync(
            new PaymentInitInput(
                reference, owed, email, null, PaymentReturnLinks.ArtisanSettlement), ct);

        var payment = Payment.InitiateSettlement(
            artisanUserId: artisanUserId,
            artisanProfileId: profile.Id,
            amountNaira: owed,
            provider: _gateway.Provider,
            reference: result.Reference,
            authorizationUrl: result.AuthorizationUrl,
            now: _clock.UtcNow);

        _payments.Add(payment);
        await _payments.SaveChangesAsync(ct);

        return new PaymentInitResponse(
            payment.Id, payment.Status.ToString(), payment.Reference,
            payment.AuthorizationUrl, payment.AmountNaira);
    }
}
