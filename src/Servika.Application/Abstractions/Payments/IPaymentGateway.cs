namespace Servika.Application.Abstractions.Payments;

/// <summary>
/// Port over a payment provider (Paystack, Monnify, Flutterwave, …). Infrastructure
/// supplies the implementation; a stub stands in when no keys are configured so the
/// flow is fully testable locally. The Application layer stays unaware of any
/// provider's HTTP shape or signing scheme.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Provider slug recorded on the payment, e.g. "paystack" / "stub".</summary>
    string Provider { get; }

    /// <summary>Starts a charge and returns where to send the customer to pay.</summary>
    Task<PaymentInitResult> InitializeAsync(PaymentInitInput input, CancellationToken ct);

    /// <summary>True if a webhook body genuinely came from the provider (HMAC etc.).</summary>
    bool VerifySignature(string rawBody, string? signature);

    /// <summary>Parses a verified webhook body into a normalized event, or null if
    /// it isn't a payment event we care about.</summary>
    PaymentWebhookEvent? ParseWebhook(string rawBody);

    /// <summary>Sends real money back to the customer who paid (e.g. a dispute
    /// resolved in their favour). Refunds the charge identified by our original
    /// <paramref name="reference"/> for <paramref name="amountNaira"/>. Best-effort:
    /// the caller records the refund in the ledger regardless, so a transient
    /// provider error is surfaced via the result, not by aborting the resolution.</summary>
    Task<GatewayRefundResult> RefundAsync(string reference, int amountNaira, CancellationToken ct);

    /// <summary>Parses a verified <c>refund.*</c> webhook into a normalized event,
    /// or null if it isn't a refund settlement we act on. A refund is requested
    /// synchronously but settles asynchronously — this confirms it landed or failed.</summary>
    RefundWebhookEvent? ParseRefundWebhook(string rawBody);
}

/// <summary>Outcome of asking the provider to refund a charge. <c>Accepted</c> means
/// the provider took the request (the money movement itself may still settle async).</summary>
public sealed record GatewayRefundResult(bool Accepted, string? Error);

public enum RefundWebhookOutcome
{
    Processed,
    Failed,
}

/// <summary>A normalized refund webhook: keyed by the ORIGINAL charge reference
/// (our <c>Payment.Reference</c>), and whether the money movement settled or failed.</summary>
public sealed record RefundWebhookEvent(string TransactionReference, RefundWebhookOutcome Outcome);

/// <summary>Inputs to start a charge. The amount is decided server-side.</summary>
public sealed record PaymentInitInput(
    string Reference,
    int AmountNaira,
    string CustomerEmail,
    /// <summary>The booking being paid — null for a commission settlement.</summary>
    Guid? BookingId);

/// <summary>The provider's reference (may differ from ours) and hosted checkout URL.</summary>
public sealed record PaymentInitResult(string Reference, string? AuthorizationUrl);

public enum PaymentWebhookOutcome
{
    Succeeded,
    Failed,
}

/// <summary>A normalized payment webhook: which reference, and how it resolved.</summary>
public sealed record PaymentWebhookEvent(string Reference, PaymentWebhookOutcome Outcome);
