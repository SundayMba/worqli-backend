using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Paystack implementation of <see cref="IPaymentGateway"/>. Initializes a
/// transaction (amount sent in kobo), verifies webhooks with HMAC-SHA512 over the
/// raw body using the secret key, and normalizes <c>charge.success</c>/failure
/// events. Selected by DI only when a Paystack key is configured.
/// </summary>
public sealed class PaystackPaymentGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackPaymentGateway> _logger;

    public PaystackPaymentGateway(
        IHttpClientFactory httpClientFactory,
        PaystackOptions options,
        ILogger<PaystackPaymentGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public string Provider => "paystack";

    public async Task<PaymentInitResult> InitializeAsync(
        PaymentInitInput input, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("paystack");
        client.BaseAddress = new Uri(_options.BaseUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

        var body = JsonSerializer.Serialize(new
        {
            email = input.CustomerEmail,
            amount = input.AmountNaira * 100, // Paystack expects kobo
            reference = input.Reference,
            // Sends the payer back into the app when the charge completes (the app
            // scheme is registered, so the checkout page hands off to the app).
            callback_url = input.CallbackUrl,
            // Where the checkout's own Cancel goes: the same link flagged cancelled,
            // so the in-app checkout screen can close cleanly instead of hanging.
            metadata = input.CallbackUrl is null ? null : new
            {
                cancel_action = input.CallbackUrl + (input.CallbackUrl.Contains('?') ? "&" : "?") + "cancelled=1",
            },
        });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/transaction/initialize", content, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Paystack init failed ({Status}): {Body}", response.StatusCode, json);
            throw new InvalidOperationException("Payment gateway could not start the transaction.");
        }

        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        var authUrl = data.GetProperty("authorization_url").GetString();
        var reference = data.TryGetProperty("reference", out var r)
            ? r.GetString() ?? input.Reference
            : input.Reference;

        return new PaymentInitResult(reference, authUrl);
    }

    public async Task<GatewayRefundResult> RefundAsync(
        string reference, int amountNaira, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("paystack");
        client.BaseAddress = new Uri(_options.BaseUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

        // Paystack refunds by the ORIGINAL transaction reference; amount in kobo.
        var body = JsonSerializer.Serialize(new
        {
            transaction = reference,
            amount = amountNaira * 100,
        });

        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/refund", content, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Paystack refund failed for {Reference} ({Status}): {Body}",
                    reference, response.StatusCode, json);
                return new GatewayRefundResult(false, $"Paystack returned {(int)response.StatusCode}");
            }

            _logger.LogInformation("Paystack refund accepted for {Reference} (₦{Amount})",
                reference, amountNaira);
            return new GatewayRefundResult(true, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Paystack refund threw for {Reference}", reference);
            return new GatewayRefundResult(false, ex.Message);
        }
    }

    public RefundWebhookEvent? ParseRefundWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(evt) || !evt.StartsWith("refund.", StringComparison.Ordinal))
                return null;

            if (!root.TryGetProperty("data", out var data))
                return null;

            // Paystack's refund payload carries the ORIGINAL charge reference so we
            // can match it to our Payment. Field name is transaction_reference.
            var reference =
                (data.TryGetProperty("transaction_reference", out var tr) ? tr.GetString() : null)
                ?? (data.TryGetProperty("transaction", out var tx) && tx.ValueKind == JsonValueKind.Object
                    && tx.TryGetProperty("reference", out var txr) ? txr.GetString() : null);
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            // event is refund.processed / refund.failed / refund.pending / refund.processing.
            // We only act on the two terminal ones; anything else is a no-op.
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

    public async Task<PaymentWebhookEvent?> VerifyAsync(string reference, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("paystack");
            client.BaseAddress = new Uri(_options.BaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);
            using var response = await client.GetAsync($"/transaction/verify/{Uri.EscapeDataString(reference)}", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                // 404 = Paystack has not seen the charge yet; anything else is logged.
                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                    _logger.LogWarning("Paystack verify failed ({Status}) for {Reference}: {Body}", response.StatusCode, reference, json);
                return null;
            }
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;
            var status = data.TryGetProperty("status", out var st) ? st.GetString() : null;
            var outcome = status switch
            {
                "success" => PaymentWebhookOutcome.Succeeded,
                "failed" or "abandoned" or "reversed" => PaymentWebhookOutcome.Failed,
                _ => PaymentWebhookOutcome.Pending, // ongoing / pending / processing / queued
            };
            long? amount = data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number ? am.GetInt64() : null;
            long? fees = data.TryGetProperty("fees", out var fe) && fe.ValueKind == JsonValueKind.Number ? fe.GetInt64() : null;
            var currency = data.TryGetProperty("currency", out var cu) ? cu.GetString() : null;
            return new PaymentWebhookEvent(reference, outcome, amount, fees, currency);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Paystack verify threw for {Reference}", reference);
            return null;
        }
    }

    public bool VerifySignature(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        // Constant-time comparison to avoid leaking via timing.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public PaymentWebhookEvent? ParseWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(evt) || !evt.StartsWith("charge.", StringComparison.Ordinal))
                return null;

            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("reference", out var refEl))
                return null;

            var reference = refEl.GetString();
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            var outcome = evt == "charge.success"
                ? PaymentWebhookOutcome.Succeeded
                : PaymentWebhookOutcome.Failed;

            long? amount = data.TryGetProperty("amount", out var am) && am.ValueKind == JsonValueKind.Number ? am.GetInt64() : null;
            long? fees = data.TryGetProperty("fees", out var fe) && fe.ValueKind == JsonValueKind.Number ? fe.GetInt64() : null;
            var currency = data.TryGetProperty("currency", out var cu) ? cu.GetString() : null;

            return new PaymentWebhookEvent(reference, outcome, amount, fees, currency);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
