using Servika.Application.Abstractions.Payments;
using Servika.Contracts.Payments;

namespace Servika.Application.Payments;

/// <summary>Lists payout-destination banks (name + code) for the withdrawal picker.</summary>
public sealed class GetBanksHandler
{
    private readonly IBankDirectory _banks;

    public GetBanksHandler(IBankDirectory banks)
    {
        _banks = banks;
    }

    public async Task<IReadOnlyList<BankDto>> HandleAsync(CancellationToken ct)
    {
        var banks = await _banks.ListBanksAsync(ct);
        return banks.Select(b => new BankDto(b.Name, b.Code)).ToList();
    }
}
