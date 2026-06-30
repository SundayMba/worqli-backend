using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Payments;

namespace Servika.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IPaymentRepository"/>. Writes are
/// tracked so status transitions persist; the webhook looks up by reference.</summary>
public sealed class PaymentRepository : IPaymentRepository
{
    private readonly ServikaDbContext _db;

    public PaymentRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(Payment payment) => _db.Payments.Add(payment);

    public Task<Payment?> FindByReferenceAsync(string reference, CancellationToken ct) =>
        _db.Payments.FirstOrDefaultAsync(p => p.Reference == reference, ct);

    public Task<Payment?> FindActiveForBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.Payments
            .Where(p => p.BookingId == bookingId && p.Status != PaymentStatus.Failed)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
