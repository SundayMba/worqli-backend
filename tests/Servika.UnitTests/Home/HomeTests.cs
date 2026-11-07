using Servika.Application.Abstractions.Persistence;
using Servika.Application.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.UnitTests.Home;

public class HomeTests
{
    private static ArtisanWorkRules Rules(double? lat, double? lng, int radius = 8) => new(lat, lng, radius, true, null);

    [Fact]
    public void Covers_any_point_the_open_pool_reaches_whatever_the_alert_radius()
    {
        var lekki = Rules(6.4474, 3.4700, radius: 8);
        Assert.True(GetAvailabilityHandler.Covers(lekki, 6.4500, 3.4800));      // ~1 km
        Assert.True(GetAvailabilityHandler.Covers(lekki, 6.6000, 3.3500));      // ~21 km, outside the alert radius
        Assert.False(GetAvailabilityHandler.Covers(lekki, 7.3775, 3.9470));     // Ibadan, ~115 km
    }

    [Fact]
    public void No_point_counts_everyone_but_an_unpinned_artisan_covers_no_known_point()
    {
        Assert.False(GetAvailabilityHandler.Covers(Rules(null, null), 6.6, 3.35));
        Assert.True(GetAvailabilityHandler.Covers(Rules(null, null), null, null));
        Assert.True(GetAvailabilityHandler.Covers(Rules(6.4474, 3.47), null, null));
        Assert.False(GetAvailabilityHandler.Covers(Rules(6.4474, 3.47, radius: 0), 9.0, 7.4));   // Abuja is out of reach
    }

    [Fact]
    public void Interest_needs_a_category_or_a_note_and_bounds_the_note()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => ServiceInterest.Record(null, "  ", null, null, null, null, now));
        Assert.Throws<ArgumentException>(() => ServiceInterest.Record(null, new string('x', 301), null, null, null, null, now));
        var i = ServiceInterest.Record(" Painting ", null, "Ikate", 6.43, 3.48, null, now);
        Assert.Equal("painting", i.CategorySlug);
        Assert.Null(i.UserId);
    }
}
