using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.Infrastructure.Persistence;

public sealed class CustomerRatingRepository : ICustomerRatingRepository
{
    private readonly ServikaDbContext _db;

    public CustomerRatingRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public Task<CustomerRating?> FindByBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.CustomerRatings.AsNoTracking().FirstOrDefaultAsync(r => r.BookingId == bookingId, ct);

    public void Add(CustomerRating rating) => _db.CustomerRatings.Add(rating);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
