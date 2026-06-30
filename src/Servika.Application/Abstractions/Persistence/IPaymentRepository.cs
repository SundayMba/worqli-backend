using Servika.Domain.Payments;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for payments. Lookups by our gateway reference power the
/// (idempotent) webhook handler.</summary>
public interface IPaymentRepository
{
    void Add(Payment payment);

    Task<Payment?> FindByReferenceAsync(string reference, CancellationToken ct);

    /// <summary>The most recent non-failed payment for a booking, if any — used to
    /// avoid double-charging an already-paid booking.</summary>
    Task<Payment?> FindActiveForBookingAsync(Guid bookingId, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
