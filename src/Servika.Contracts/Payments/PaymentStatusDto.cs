namespace Servika.Contracts.Payments;

/// <summary>
/// Where a payment stands (POST /api/v1/payments/{reference}/verify). The app asks
/// the moment the payer returns from the bank or wallet app, so it does not have to
/// wait for the webhook. <see cref="Status"/> is "Pending" / "Succeeded" / "Failed" /
/// "Refunded"; <see cref="BookingPaymentState"/> is the booking's view when there is one.
/// </summary>
public sealed record PaymentStatusDto(
    Guid PaymentId,
    string Reference,
    string Status,
    int AmountNaira,
    int ServiceFeeNaira,
    int TotalNaira,
    Guid? BookingId,
    string? BookingPaymentState,
    /// <summary>True when this call is what settled it (the webhook had not landed yet).</summary>
    bool SettledNow);
