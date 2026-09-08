using Servika.Domain.Bookings;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Artisan → customer ratings, one per booking, private to Servika.</summary>
public interface ICustomerRatingRepository
{
    Task<CustomerRating?> FindByBookingAsync(Guid bookingId, CancellationToken ct);
    void Add(CustomerRating rating);
    Task<int> SaveChangesAsync(CancellationToken ct);
}
