using Servika.Domain.Bookings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Persistence for bookings, stated in Domain terms; Infrastructure implements it
/// with EF Core. Reads are scoped to the owning customer so one customer can never
/// see another's bookings (enforced again here, on top of the API's auth check).
/// </summary>
public interface IBookingRepository
{
    void Add(Booking booking);

    /// <summary>The customer's bookings, newest first, optionally one status only.</summary>
    Task<IReadOnlyList<Booking>> ListForCustomerAsync(
        Guid customerId, BookingStatus? status, CancellationToken ct);

    /// <summary>A single booking owned by this customer, or null if not found.</summary>
    Task<Booking?> FindForCustomerAsync(Guid id, Guid customerId, CancellationToken ct);

    /// <summary>The jobs assigned to one artisan profile, newest first, optionally
    /// one status only. The artisan's mirror of <see cref="ListForCustomerAsync"/>.</summary>
    Task<IReadOnlyList<Booking>> ListForArtisanAsync(
        Guid artisanProfileId, BookingStatus? status, CancellationToken ct);

    /// <summary>A single booking assigned to this artisan profile, or null if not
    /// found. Tracked so the artisan's state-machine transitions persist.</summary>
    Task<Booking?> FindForArtisanAsync(Guid id, Guid artisanProfileId, CancellationToken ct);

    /// <summary>A booking by id with no customer scoping — for internal flows that
    /// already trust the source (e.g. applying a verified payment webhook).</summary>
    Task<Booking?> FindByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Every booking, newest first, optionally one status only — admin
    /// oversight (unscoped). Read-only.</summary>
    Task<IReadOnlyList<Booking>> ListAllAsync(BookingStatus? status, CancellationToken ct);

    /// <summary>Bookings stuck AwaitingConfirmation since before <paramref name="cutoffUtc"/>
    /// — for the auto-confirm sweep. Tracked so the transition persists.</summary>
    Task<IReadOnlyList<Booking>> ListAwaitingConfirmationBeforeAsync(
        DateTimeOffset cutoffUtc, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
