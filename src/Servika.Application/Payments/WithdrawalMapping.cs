using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>Maps a withdrawal Domain entity to its public DTO.</summary>
internal static class WithdrawalMapping
{
    public static WithdrawalDto ToDto(this Withdrawal w) =>
        new(w.Id, w.AmountNaira, w.Status.ToString(), w.Method, w.BankName,
            w.AccountNumberMasked, w.AccountName, w.CreatedAt, w.ProcessedAtUtc,
            w.FeeNaira, w.FeeBearer.ToString(), w.NetNaira);
}
