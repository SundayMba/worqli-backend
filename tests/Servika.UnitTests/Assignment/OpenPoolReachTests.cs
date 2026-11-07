using Servika.Application.Bookings;
using Servika.Domain.Bookings;

namespace Servika.UnitTests.Assignment;

/// <summary>The open pool reaches across the state; the work radius only decides who is alerted.</summary>
public class OpenPoolReachTests
{
    // TestData.OpenJob() sits in Lekki Phase 1 (~6.447, 3.471).
    [Fact]
    public void Ajah_artisan_with_a_small_radius_still_sees_a_Lekki_job_but_gets_no_alert()
    {
        var ajah = TestData.Artisan("Ajah", 6.4680, 3.5850, radiusKm: 8);   // ~13 km east
        var job = TestData.OpenJob();
        Assert.True(OpenJobMatching.PoolAccepts(ajah.Rules, job, TestData.Now));
        Assert.False(OpenJobMatching.Accepts(ajah.Rules, job, TestData.Now));
    }

    [Fact]
    public void Jobs_beyond_the_pool_reach_are_left_out()
    {
        var ibadan = TestData.Artisan("Ibadan", 7.3775, 3.9470);   // ~115 km away
        Assert.False(OpenJobMatching.PoolAccepts(ibadan.Rules, TestData.OpenJob(), TestData.Now));
    }

    [Fact]
    public void Away_and_no_urgent_work_still_apply_to_the_pool()
    {
        var away = TestData.Artisan("Away", 6.4480, 3.4710, away: TestData.Now.AddDays(1));
        var calm = TestData.Artisan("Calm", 6.4480, 3.4710, emergency: false);
        Assert.False(OpenJobMatching.PoolAccepts(away.Rules, TestData.OpenJob(), TestData.Now));
        Assert.False(OpenJobMatching.PoolAccepts(calm.Rules, TestData.OpenJob(urgency: Urgency.Urgent), TestData.Now));
    }

    [Fact]
    public void Distance_is_unknown_without_both_pins()
    {
        var a = TestData.Artisan("A", 6.4680, 3.5850);
        Assert.Null(OpenJobMatching.DistanceKm(a.Rules, TestData.OpenJob(null, null)));
        Assert.InRange(OpenJobMatching.DistanceKm(a.Rules, TestData.OpenJob())!.Value, 10, 16);
    }
}
