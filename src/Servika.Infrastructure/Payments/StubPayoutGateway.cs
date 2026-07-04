using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Dev/test stand-in for a real transfer provider (used until Paystack Transfers is
/// wired). Every disbursement "succeeds" synchronously and returns a fake transfer
/// reference, so the artisan withdrawal flow is fully curl-testable locally without
/// credentials. A real provider would create a transfer recipient + initiate the
/// transfer and confirm via a transfer webhook.
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
}
