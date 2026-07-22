using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Paystack Transfers implementation of <see cref="IPayoutGateway"/>. Disbursing is
/// two calls — create a transfer recipient (NUBAN: bank code + account number), then
/// initiate the transfer (amount in kobo, our withdrawal id as the reference). The
/// transfer is <b>asynchronous</b>: Paystack accepts it (returns Pending) and the
/// real result arrives on a <c>transfer.success</c> / <c>transfer.failed</c> /
/// <c>transfer.reversed</c> webhook, verified with the same HMAC-SHA512 secret as
/// charges. Selected by DI only when a Paystack key is configured.
///
/// <para>⚠️ Requires "Transfers OTP" to be OFF in the Paystack dashboard (otherwise
/// each transfer needs an OTP and can't complete automatically), and the business
/// to be approved for Transfers with a funded balance.</para>
/// </summary>
public sealed class PaystackPayoutGateway : IPayoutGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackPayoutGateway> _logger;

    public PaystackPayoutGateway(
        IHttpClientFactory httpClientFactory,
        PaystackOptions options,
        ILogger<PaystackPayoutGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public string Provider => "paystack";

    public async Task<PayoutResult> DisburseAsync(PayoutInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.BankCode))
            return new PayoutResult(PayoutOutcome.Failed, null,
                "No bank selected — pick your bank from the list and try again.");

        var client = _httpClientFactory.CreateClient("paystack");
        client.BaseAddress = new Uri(_options.BaseUrl);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

        try
        {
            // 1) Create a transfer recipient for this bank account.
            var recipientBody = JsonSerializer.Serialize(new
            {
                type = "nuban",
                name = input.AccountName,
                account_number = input.AccountNumber,
                bank_code = input.BankCode,
                currency = "NGN",
            });
            using var recipientContent = new StringContent(recipientBody, Encoding.UTF8, "application/json");
            using var recipientResp = await client.PostAsync("/transferrecipient", recipientContent, ct);
            var recipientJson = await recipientResp.Content.ReadAsStringAsync(ct);
            if (!recipientResp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack recipient failed ({Status}): {Body}", recipientResp.StatusCode, recipientJson);
                return new PayoutResult(PayoutOutcome.Failed, null,
                    "We couldn't verify that bank account. Check the details and try again.");
            }

            using var recipientDoc = JsonDocument.Parse(recipientJson);
            var recipientCode = recipientDoc.RootElement
                .GetProperty("data").GetProperty("recipient_code").GetString();
            if (string.IsNullOrWhiteSpace(recipientCode))
                return new PayoutResult(PayoutOutcome.Failed, null, "Bank account could not be set up.");

            // 2) Initiate the transfer. Reference = our withdrawal id (webhook key).
            var transferBody = JsonSerializer.Serialize(new
            {
                source = "balance",
                amount = input.AmountNaira * 100, // kobo
                recipient = recipientCode,
                reference = input.Reference,
                reason = "Servika payout",
            });
            using var transferContent = new StringContent(transferBody, Encoding.UTF8, "application/json");
            using var transferResp = await client.PostAsync("/transfer", transferContent, ct);
            var transferJson = await transferResp.Content.ReadAsStringAsync(ct);
            if (!transferResp.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack transfer failed ({Status}): {Body}", transferResp.StatusCode, transferJson);
                return new PayoutResult(PayoutOutcome.Failed, null,
                    "The transfer couldn't be started. Your balance was not touched.");
            }

            using var transferDoc = JsonDocument.Parse(transferJson);
            var data = transferDoc.RootElement.GetProperty("data");
            var transferCode = data.TryGetProperty("transfer_code", out var tc) ? tc.GetString() : null;
            var status = data.TryGetProperty("status", out var st) ? st.GetString() : "pending";

            // "success" can come back immediately in test mode; anything else that
            // wasn't an HTTP error means accepted-and-pending → settle on the webhook.
            var outcome = status == "success" ? PayoutOutcome.Succeeded : PayoutOutcome.Pending;
            return new PayoutResult(outcome, transferCode, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack payout threw for reference {Reference}", input.Reference);
            return new PayoutResult(PayoutOutcome.Failed, null,
                "The transfer couldn't be started. Your balance was not touched.");
        }
    }

    public bool VerifySignature(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
            return false;

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_options.SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    public TransferWebhookEvent? ParseTransferWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            if (string.IsNullOrWhiteSpace(evt) || !evt.StartsWith("transfer.", StringComparison.Ordinal))
                return null;

            if (!root.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("reference", out var refEl))
                return null;

            var reference = refEl.GetString();
            if (string.IsNullOrWhiteSpace(reference))
                return null;

            // transfer.success settles; transfer.failed / transfer.reversed reverse.
            var outcome = evt == "transfer.success"
                ? PayoutOutcome.Succeeded
                : PayoutOutcome.Failed;

            return new TransferWebhookEvent(reference, outcome);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
