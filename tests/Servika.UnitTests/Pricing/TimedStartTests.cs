using Servika.Application.Bookings;
using Servika.Domain.Bookings;
using Servika.UnitTests.Assignment;
using Xunit;

namespace Servika.UnitTests.Pricing;

/// <summary>Late starts against the promised "free from" time, the busy window, and the job cap's wording.</summary>
public class TimedStartTests
{
    private static Booking Picked(DateTimeOffset? promised)
    {
        var b = TestData.OpenJob();
        b.AcceptVisitBid(Guid.NewGuid(), "Emeka", 0, TestData.Now);
        b.SetPromisedStart(promised);
        return b;
    }

    [Fact]
    public void Setting_off_within_the_grace_is_on_time()
    {
        var b = Picked(TestData.Now.AddHours(1));
        b.StartTrip(TestData.Now.AddHours(1).AddMinutes(25));
        Assert.False(b.StartedLate);
        Assert.Equal(TestData.Now.AddHours(1).AddMinutes(25), b.TripStartedAtUtc);
    }

    [Fact]
    public void Setting_off_well_after_the_promise_is_late()
    {
        var b = Picked(TestData.Now.AddHours(1));
        b.StartTrip(TestData.Now.AddHours(2));
        Assert.True(b.StartedLate);
    }

    [Fact]
    public void No_promise_is_never_late()
    {
        var b = Picked(null);
        b.StartTrip(TestData.Now.AddDays(1));
        Assert.False(b.StartedLate);
    }

    [Fact]
    public void Arrival_time_is_recorded()
    {
        var b = Picked(null);
        b.StartTrip(TestData.Now);
        b.Arrive(TestData.Now.AddMinutes(40));
        Assert.Equal(TestData.Now.AddMinutes(40), b.ArrivedAtUtc);
    }

    [Fact]
    public void Busy_windows_cover_a_working_day_but_not_for_ever()
    {
        Assert.Equal(TimeSpan.FromHours(12), ArtisanBusy.ArrivedCountsFor);
        Assert.Equal(TimeSpan.FromHours(24), ArtisanBusy.WorkingCountsFor);
    }

    [Fact]
    public void Cap_wording_speaks_to_each_side()
    {
        Assert.Contains("3 jobs on the go", ArtisanBusy.CapReached(3));
        Assert.Contains("1 job on the go", ArtisanBusy.CapReached(1));
        Assert.StartsWith("Emeka has as many jobs", ArtisanBusy.CapReachedForCustomer("Emeka Okafor"));
    }
}
