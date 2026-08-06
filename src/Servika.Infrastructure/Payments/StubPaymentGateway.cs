using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Dev/test stand-in for a real gateway (used when no Paystack key is configured).
/// "Checkout" is a fake deep link, signatures are accepted, and a webhook is just a
/// tiny JSON body <c>{ "reference": "...", "status": "success|failed" }</c> — so the
/// whole escrow + ledger flow is curl-testable locally without credentials.
/// </summary>
public sealed class StubPaymentGateway : IPaymentGateway
{
    private readonly ILogger<StubPaymentGateway> _logger;

    public StubPaymentGateway(ILogger<StubPaymentGateway> logger)
    {
        _logger = logger;
    }

    public string Provider => "stub";

    public Task<PaymentInitResult> InitializeAsync(PaymentInitInput input, CancellationToken ct)
    {
        _logger.LogInformation(
            "[STUB-PAY] initialized {Reference} for ₦{Amount} (booking {BookingId}). " +
            "Simulate success: POST /api/v1/payments/webhook {{\"reference\":\"{Reference}\",\"status\":\"success\"}}",
            input.Reference, input.AmountNaira, input.BookingId, input.Reference);

        return Task.FromResult(new PaymentInitResult(
            input.Reference,
            $"servika://mock-pay/{input.Reference}"));
    }

    public Task<GatewayRefundResult> RefundAsync(
        string reference, int amountNaira, CancellationToken ct)
    {
        _logger.LogInformation(
            "[STUB-PAY] refunded {Reference} for ₦{Amount} (no real money moves).",
            reference, amountNaira);
        return Task.FromResult(new GatewayRefundResult(true, null));
    }

    // Dev gateway: every body is trusted.
    public bool VerifySignature(string rawBody, string? signature) => true;

    public PaymentWebhookEvent? ParseWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (!root.TryGetProperty("reference", out var refEl))
                return null;

            var reference = refEl.GetString();
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            var status = root.TryGetProperty("status", out var st) ? st.GetString() : "success";
            var outcome = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                ? PaymentWebhookOutcome.Succeeded
                : PaymentWebhookOutcome.Failed;

            return new PaymentWebhookEvent(reference, outcome);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
