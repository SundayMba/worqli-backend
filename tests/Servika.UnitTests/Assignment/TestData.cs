using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Bookings;

namespace Servika.UnitTests.Assignment;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);
    // Lekki Phase 1
    public const double JobLat = 6.4474, JobLng = 3.4700;

    public static Booking OpenJob(double? lat = JobLat, double? lng = JobLng, Urgency urgency = Urgency.Standard) =>
        Booking.Create(Guid.NewGuid(), null, "electrical", "Electrical", null, "Socket sparks", "12 Admiralty Way, Lekki Phase 1",
            lat, lng, null, Now.AddDays(1), "Morning", urgency, PricingModel.Variable, null, 0m, Now);

    public static ArtisanRecipient Artisan(string name, double lat, double lng, DateTimeOffset? lastAssigned = null,
        int radiusKm = 8, bool emergency = true, DateTimeOffset? away = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new ArtisanWorkRules(lat, lng, radiusKm, emergency, away), name, lastAssigned);
}
