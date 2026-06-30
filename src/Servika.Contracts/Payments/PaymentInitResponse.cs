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
    int AmountNaira);
