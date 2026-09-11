namespace Servika.Contracts.Payments;

/// <summary>
/// Result of initializing a booking payment (POST /payments/bookings/{id}/initialize).
/// The client opens <see cref="AuthorizationUrl"/> (gateway-hosted checkout) and the
/// final result arrives asynchronously via the gateway webhook. <see cref="Status"/>
/// is a readable string (initially "Pending").
/// </summary>
public sealed record PaymentInitResponse(
    Guid PaymentId,
    string Status,
    string Reference,
    string? AuthorizationUrl,
    /// <summary>The agreed price going into escrow.</summary>
    int AmountNaira,
    /// <summary>The payment fee added on top (0 while Servika covers fees).</summary>
    int ServiceFeeNaira = 0,
    /// <summary>What the card is charged: amount + service fee.</summary>
    int TotalNaira = 0);
