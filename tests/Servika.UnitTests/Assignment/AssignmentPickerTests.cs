using Servika.Application.Bookings;
using Servika.Domain.Bookings;

namespace Servika.UnitTests.Assignment;

public class AssignmentPickerTests
{
    private static readonly HashSet<Guid> Nobody = new();

    [Fact]
    public void Least_recently_assigned_goes_first_whatever_the_distance()
    {
        var near = TestData.Artisan("Near", 6.4480, 3.4710, lastAssigned: TestData.Now.AddHours(-1));   // ~0.1 km, assigned an hour ago
        var far = TestData.Artisan("Far", 6.4900, 3.5200, lastAssigned: TestData.Now.AddDays(-3));      // ~7 km, assigned days ago
        var never = TestData.Artisan("Never", 6.4700, 3.5000);                                           // ~4 km, never assigned
        var order = AssignmentPicker.Order(new[] { near, far, never }, TestData.OpenJob(), Nobody, TestData.Now);
        Assert.Equal(new[] { "Never", "Far", "Near" }, order.Select(r => r.FullName));
    }

    [Fact]
    public void Nearest_breaks_the_tie_between_equally_rested_artisans()
    {
        var a = TestData.Artisan("A 4km", 6.4700, 3.5000);
        var b = TestData.Artisan("B 0.1km", 6.4480, 3.4710);
        var c = TestData.Artisan("C unknown", 0, 0);
        c = c with { Rules = c.Rules with { Latitude = null, Longitude = null } };
        var order = AssignmentPicker.Order(new[] { a, c, b }, TestData.OpenJob(), Nobody, TestData.Now);
        Assert.Equal(new[] { "B 0.1km", "A 4km", "C unknown" }, order.Select(r => r.FullName));
    }

    [Fact]
    public void Excluded_artisans_are_skipped()
    {
        var a = TestData.Artisan("A", 6.4480, 3.4710);
        var b = TestData.Artisan("B", 6.4700, 3.5000);
        var order = AssignmentPicker.Order(new[] { a, b }, TestData.OpenJob(), new HashSet<Guid> { a.ProfileId }, TestData.Now);
        Assert.Equal(new[] { "B" }, order.Select(r => r.FullName));
    }

    [Fact]
    public void Own_rules_filter_away_radius_and_emergency()
    {
        var away = TestData.Artisan("Away", 6.4480, 3.4710, away: TestData.Now.AddDays(2));
        var outside = TestData.Artisan("Outside", 6.6000, 3.3500, radiusKm: 5);                     // ~21 km
        var noEmergency = TestData.Artisan("Calm", 6.4480, 3.4710, emergency: false);
        var ok = TestData.Artisan("Ok", 6.4500, 3.4720);
        var urgent = TestData.OpenJob(urgency: Urgency.Urgent);
        var order = AssignmentPicker.Order(new[] { away, outside, noEmergency, ok }, urgent, Nobody, TestData.Now);
        Assert.Equal(new[] { "Ok" }, order.Select(r => r.FullName));
    }

    [Fact]
    public void Radius_is_ignored_when_the_job_has_no_pin()
    {
        var outside = TestData.Artisan("Outside", 6.6000, 3.3500, radiusKm: 5);
        var order = AssignmentPicker.Order(new[] { outside }, TestData.OpenJob(null, null), Nobody, TestData.Now);
        Assert.Single(order);
    }
}
