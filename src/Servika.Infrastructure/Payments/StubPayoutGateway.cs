using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Dev/test stand-in for a real transfer provider (used when Paystack Transfers
/// isn't configured). Every disbursement "succeeds" synchronously and returns a
/// fake transfer reference, so the artisan withdrawal flow is fully curl-testable
/// locally without credentials. It also accepts any transfer webhook (parsing a
/// tiny <c>{reference,status}</c> body) so the async settlement path is exercisable
/// locally too. The real Paystack path creates a recipient, initiates the transfer,
/// and settles on the transfer webhook.
/// </summary>
public sealed class StubPayoutGateway : IPayoutGateway
{
    private readonly ILogger<StubPayoutGateway> _logger;

    public StubPayoutGateway(ILogger<StubPayoutGateway> logger)
    {
        _logger = logger;
    }

    public string Provider => "stub";

    public Task<PayoutResult> DisburseAsync(PayoutInput input, CancellationToken ct)
    {
        _logger.LogInformation(
            "[STUB-PAYOUT] disbursed ₦{Amount} to {Bank} {Account} ({Name}), ref {Reference}",
            input.AmountNaira, input.BankName, input.AccountNumber, input.AccountName, input.Reference);

        return Task.FromResult(new PayoutResult(
            PayoutOutcome.Succeeded, $"stub-transfer-{input.Reference}", null));
    }

    /// <summary>Stub accepts every signature (no secret configured locally).</summary>
    public bool VerifySignature(string rawBody, string? signature) => true;

    /// <summary>Accepts both a tiny <c>{"reference","status"}</c> body and the real
    /// Paystack shape <c>{"event":"transfer.*","data":{"reference"}}</c>, so the exact
    /// production webhook payload is exercisable locally against the stub.</summary>
    public TransferWebhookEvent? ParseTransferWebhook(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            // Prefer the nested Paystack shape; fall back to a flat body.
            var container = root.TryGetProperty("data", out var data) ? data : root;
            if (!container.TryGetProperty("reference", out var refEl)) return null;
            var reference = refEl.GetString();
            if (string.IsNullOrWhiteSpace(reference)) return null;

            // Outcome from the event name if present, else from a status field.
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            var status = container.TryGetProperty("status", out var s) ? s.GetString() : null;
            var failed = evt is "transfer.failed" or "transfer.reversed"
                || status is "failed" or "reversed";
            return new TransferWebhookEvent(reference, failed ? PayoutOutcome.Failed : PayoutOutcome.Succeeded);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The stub has no balance to check.
    public Task<long?> GetBalanceNairaAsync(CancellationToken ct) => Task.FromResult<long?>(null);
}
