using Servika.Domain.Disputes;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for disputes. Shares the scoped <c>ServikaDbContext</c>, so a
/// dispute and the booking state-change it triggers commit in one transaction.
/// </summary>
public interface IDisputeRepository
{
    void Add(Dispute dispute);

    /// <summary>Is there already an unresolved dispute for this booking?</summary>
    Task<bool> HasOpenForBookingAsync(Guid bookingId, CancellationToken ct);

    /// <summary>The dispute for a booking raised by this customer, or null.
    /// Untracked — drives the customer's "you reported an issue" state.</summary>
    Task<Dispute?> FindForBookingAsync(Guid bookingId, Guid raisedByUserId, CancellationToken ct);

    /// <summary>A dispute by id, unscoped and <b>tracked</b> — for the admin to
    /// resolve (its status change persists on SaveChanges).</summary>
    Task<Dispute?> FindByIdForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>All disputes, newest first, optionally one status only (admin).</summary>
    Task<IReadOnlyList<Dispute>> ListAllAsync(DisputeStatus? status, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
