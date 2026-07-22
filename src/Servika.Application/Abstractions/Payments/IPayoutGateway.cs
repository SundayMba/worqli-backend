namespace Servika.Application.Abstractions.Payments;

/// <summary>
/// Port over a bank-transfer/payout provider (Paystack Transfers, Flutterwave, …).
/// Infrastructure supplies the implementation; a stub stands in when no keys are
/// configured so the withdrawal flow is fully testable locally. Kept separate from
/// <see cref="IPaymentGateway"/> because charging a customer and disbursing to an
/// artisan are different provider APIs.
/// </summary>
public interface IPayoutGateway
{
    /// <summary>Provider slug recorded on the withdrawal, e.g. "paystack" / "stub".</summary>
    string Provider { get; }

    /// <summary>Initiates a payout to a bank account. A synchronous provider (the
    /// stub) resolves it immediately (<see cref="PayoutOutcome.Succeeded"/>); a real
    /// provider (Paystack Transfers) returns <see cref="PayoutOutcome.Pending"/> — the
    /// transfer is accepted and its result arrives later on a transfer webhook.</summary>
    Task<PayoutResult> DisburseAsync(PayoutInput input, CancellationToken ct);

    /// <summary>True if a transfer webhook body genuinely came from the provider.</summary>
    bool VerifySignature(string rawBody, string? signature);

    /// <summary>Parses a verified transfer webhook into a normalized event, or null
    /// if it isn't a transfer event we care about.</summary>
    TransferWebhookEvent? ParseTransferWebhook(string rawBody);
}

/// <summary>A normalized transfer webhook: our payout reference + how it resolved.</summary>
public sealed record TransferWebhookEvent(string Reference, PayoutOutcome Outcome);

/// <summary>Inputs to disburse a payout. The amount is decided server-side.
/// <see cref="BankCode"/> is the provider's bank code (required by real transfer
/// APIs to build a recipient); the stub ignores it.</summary>
public sealed record PayoutInput(
    string Reference,
    int AmountNaira,
    string BankName,
    string? BankCode,
    string AccountNumber,
    string AccountName);

public enum PayoutOutcome
{
    /// <summary>Disbursement fully succeeded (synchronous providers / a success webhook).</summary>
    Succeeded,

    /// <summary>Transfer accepted by the provider but not yet settled — awaiting the
    /// transfer webhook. The reservation stays; the payout remains Pending.</summary>
    Pending,

    /// <summary>Disbursement was rejected/failed/reversed (reverse the ledger reservation).</summary>
    Failed,
}

/// <summary>The provider's transfer reference and how the disbursement resolved.</summary>
public sealed record PayoutResult(PayoutOutcome Outcome, string? Reference, string? FailureReason);
