namespace Servika.Domain.Favorites;

/// <summary>
/// A customer's saved ("favourite") artisan. One row per (user, artisan) pair —
/// saving is idempotent. <see cref="ArtisanId"/> is the catalogue profile id (same
/// id space as <c>Booking.ArtisanId</c>), so it's a plain column with no FK.
/// </summary>
public sealed class Favorite
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ArtisanId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Favorite() { }

    public static Favorite Create(Guid userId, Guid artisanId, DateTimeOffset now)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User is required.", nameof(userId));
        if (artisanId == Guid.Empty)
            throw new ArgumentException("Artisan is required.", nameof(artisanId));

        return new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ArtisanId = artisanId,
            CreatedAt = now,
        };
    }
}
