using Servika.Domain.Bookings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Repairs proposed on jobs (from Servika's add-on list). Shares the booking DbContext.</summary>
public interface IBookingRepairRepository
{
    /// <summary>Every repair on a booking (tracked), oldest first.</summary>
    Task<IReadOnlyList<BookingRepair>> ListForBookingAsync(Guid bookingId, CancellationToken ct);

    Task<BookingRepair?> FindAsync(Guid bookingId, Guid repairId, CancellationToken ct);

    void Add(BookingRepair repair);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
