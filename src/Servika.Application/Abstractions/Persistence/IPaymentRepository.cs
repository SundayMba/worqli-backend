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

    /// <summary>The settled (Succeeded) payment for a booking, if any — the one a
    /// refund reverses. Tracked, so <c>MarkRefunded</c> persists.</summary>
    Task<Payment?> FindSucceededForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>All payments a refund was requested on (newest first), for the admin
    /// refunds view. Read-only.</summary>
    Task<IReadOnlyList<Payment>> ListRefundedAsync(CancellationToken ct);

    /// <summary>Settled escrow payments whose earning has not been released and that
    /// were not refunded: the money Servika is holding for jobs still in progress.
    /// Read-only; powers the admin float-health check.</summary>
    Task<IReadOnlyList<Payment>> ListHeldEscrowAsync(CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
