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

    /// <summary>Attempts to disburse a payout to a bank account.</summary>
    Task<PayoutResult> DisburseAsync(PayoutInput input, CancellationToken ct);
}

/// <summary>Inputs to disburse a payout. The amount is decided server-side.</summary>
public sealed record PayoutInput(
    string Reference,
    int AmountNaira,
    string BankName,
    string AccountNumber,
    string AccountName);

public enum PayoutOutcome
{
    /// <summary>Disbursement accepted/succeeded.</summary>
    Succeeded,

    /// <summary>Disbursement was rejected/failed (reverse the ledger reservation).</summary>
    Failed,
}

/// <summary>The provider's transfer reference and how the disbursement resolved.</summary>
public sealed record PayoutResult(PayoutOutcome Outcome, string? Reference, string? FailureReason);
