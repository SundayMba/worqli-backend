using Servika.Application.Bookings;
using Servika.Domain.Bookings;
using Servika.UnitTests.Assignment;
using Xunit;

namespace Servika.UnitTests.Pricing;

/// <summary>When an artisan can come, and the busy rule's wording.</summary>
public class BidAvailabilityTests
{
    private static Bid NewBid() =>
        Bid.Place(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Emeka", 15000, new List<BidMaterialLine>(), null, TestData.Now);

    [Fact]
    public void A_future_time_is_kept_in_utc()
    {
        var bid = NewBid();
        var at = TestData.Now.AddHours(3).ToOffset(TimeSpan.FromHours(1));
        bid.SetAvailability(at, TestData.Now);
        Assert.Equal(at.ToUniversalTime(), bid.AvailableFromUtc);
        Assert.Equal(TimeSpan.Zero, bid.AvailableFromUtc!.Value.Offset);
    }

    [Fact]
    public void A_past_time_means_now()
    {
        var bid = NewBid();
        bid.SetAvailability(TestData.Now.AddMinutes(-5), TestData.Now);
        Assert.Null(bid.AvailableFromUtc);
    }

    [Fact]
    public void More_than_two_weeks_ahead_is_refused()
    {
        var bid = NewBid();
        Assert.Throws<ArgumentException>(() => bid.SetAvailability(TestData.Now.AddDays(15), TestData.Now));
    }

    [Fact]
    public void Clearing_the_time_means_as_soon_as_picked()
    {
        var bid = NewBid();
        bid.SetAvailability(TestData.Now.AddHours(2), TestData.Now);
        bid.SetAvailability(null, TestData.Now);
        Assert.Null(bid.AvailableFromUtc);
    }

    [Fact]
    public void Busy_wording_has_no_em_dashes()
    {
        Assert.DoesNotContain('—', ArtisanBusy.NeedsTime);
    }
}
