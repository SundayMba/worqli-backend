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

    /// <summary>Dev refund webhook. Mirrors the real routed shape (an <c>event</c> of
    /// <c>refund.processed</c>/<c>refund.failed</c>) so the controller routes it here,
    /// with the original charge reference at top level: <c>{ "event": "refund.processed",
    /// "reference": "..." }</c>.</summary>
    public RefundWebhookEvent? ParseRefundWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(evt))
                return null;

            var reference =
                (root.TryGetProperty("reference", out var r) ? r.GetString() : null)
                ?? (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty("transaction_reference", out var tr) ? tr.GetString() : null);
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            return evt switch
            {
                "refund.processed" => new RefundWebhookEvent(reference, RefundWebhookOutcome.Processed),
                "refund.failed" => new RefundWebhookEvent(reference, RefundWebhookOutcome.Failed),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Dev gateway: verify says "still pending" so the app's waiting state is exercised
    // locally; the stub webhook (posted by hand) is what settles a payment in dev.
    public Task<PaymentWebhookEvent?> VerifyAsync(string reference, CancellationToken ct)
    {
        _logger.LogInformation("[STUB-PAY] verify {Reference}: pending until the stub webhook fires", reference);
        return Task.FromResult<PaymentWebhookEvent?>(new PaymentWebhookEvent(reference, PaymentWebhookOutcome.Pending));
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

            // Optional, so a test can replay a short-paid or foreign-currency charge.
            long? amount = root.TryGetProperty("amountKobo", out var am) && am.ValueKind == JsonValueKind.Number ? am.GetInt64() : null;
            long? fees = root.TryGetProperty("feesKobo", out var fe) && fe.ValueKind == JsonValueKind.Number ? fe.GetInt64() : null;
            var currency = root.TryGetProperty("currency", out var cu) ? cu.GetString() : null;

            return new PaymentWebhookEvent(reference, outcome, amount, fees, currency);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
