namespace Servika.Domain.Payments;

/// <summary>
/// Lifecycle of a single payment against a booking. Stored as a readable string.
/// A payment is born <see cref="Pending"/> at initialization and moves to a
/// terminal state when the gateway webhook arrives.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3,
}
