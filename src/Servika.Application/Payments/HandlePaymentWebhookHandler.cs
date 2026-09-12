using Servika.Application.Abstractions.Payments;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>
/// Applies a gateway payment webhook. The signature is verified first (a forged
/// body is a 401), then the event is matched to our Pending payment by reference
/// and handed to <see cref="PaymentSettlementService"/>, the same code the verify
/// endpoint uses. <b>Idempotent</b>: an unknown reference or an already-settled
/// payment is a no-op, so the gateway can safely retry.
/// </summary>
public sealed class HandlePaymentWebhookHandler
{
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly PaymentSettlementService _settlement;

    public HandlePaymentWebhookHandler(
        IPaymentRepository payments,
        IPaymentGateway gateway,
        PaymentSettlementService settlement)
    {
        _payments = payments;
        _gateway = gateway;
        _settlement = settlement;
    }

    public async Task HandleAsync(string rawBody, string? signature, CancellationToken ct)
    {
        if (!_gateway.VerifySignature(rawBody, signature))
            throw new InvalidWebhookSignatureException();

        var evt = _gateway.ParseWebhook(rawBody);
        if (evt is null)
            return; // not an event we care about

        var payment = await _payments.FindByReferenceAsync(evt.Reference, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return; // unknown or already settled: idempotent no-op

        if (await _settlement.ApplyAsync(payment, evt, ct))
            await _payments.SaveChangesAsync(ct);
    }
}
