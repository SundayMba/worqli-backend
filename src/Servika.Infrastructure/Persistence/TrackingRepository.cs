using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Tracking;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="ITrackingRepository"/>. Active-session
/// look-ups are tracked so location updates persist; the stale query reads the
/// active sessions whose freshness (last update, or start when never updated) has
/// passed the cutoff.
/// </summary>
public sealed class TrackingRepository : ITrackingRepository
{
    private readonly ServikaDbContext _db;

    public TrackingRepository(ServikaDbContext db)
    {
        _db = db;
    }

    public void Add(TrackingSession session) => _db.TrackingSessions.Add(session);

    public Task<TrackingSession?> FindActiveByBookingAsync(Guid bookingId, CancellationToken ct) =>
        _db.TrackingSessions.FirstOrDefaultAsync(
            s => s.BookingId == bookingId && s.Status == TrackingStatus.Active, ct);

    public async Task<IReadOnlyList<TrackingSession>> ListStaleActiveAsync(
        DateTimeOffset cutoffUtc, CancellationToken ct) =>
        await _db.TrackingSessions
            .Where(s => s.Status == TrackingStatus.Active
                && (s.LastUpdateAtUtc ?? s.StartedAtUtc) < cutoffUtc)
            .ToListAsync(ct);

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
