using Servika.Contracts.Payments;
using Servika.Domain.Payments;

namespace Servika.Application.Payments;

/// <summary>Maps payment/wallet Domain entities to the public Contracts DTOs.</summary>
internal static class PaymentMapping
{
    public static WalletTransactionDto ToDto(this WalletTransaction t) =>
        new(t.Id, t.Type.ToString(), t.AmountNaira, t.BookingId, t.Description, t.CreatedAt);
}
