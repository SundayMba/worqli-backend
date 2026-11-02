using Servika.Domain.Bookings;
using Servika.Domain.Settings;

namespace Servika.UnitTests.Assignment;

public class BookingOffersTests
{
    [Fact]
    public void Open_requests_are_never_put_out_for_bids()
    {
        var b = Booking.Create(Guid.NewGuid(), null, "electrical", "Electrical", null, "x", "Lekki", null, null, null,
            TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Variable, null, 0m, TestData.Now,
            assessment: AssessmentMode.RemoteQuote);
        Assert.Equal(BookingStatus.Open, b.Status);
        Assert.Equal(AssessmentMode.Inspection, b.Assessment);
    }

    [Fact]
    public void Offers_are_counted_and_exhaustion_clears_on_the_next_offer()
    {
        var b = TestData.OpenJob();
        b.RecordOffer(TestData.Now);
        b.RecordOffer(TestData.Now.AddMinutes(10));
        Assert.Equal(2, b.OfferRound);
        b.MarkOffersExhausted(TestData.Now.AddMinutes(20));
        Assert.NotNull(b.OffersExhaustedAtUtc);
        b.RecordOffer(TestData.Now.AddMinutes(40));
        Assert.Null(b.OffersExhaustedAtUtc);
        Assert.Equal(3, b.OfferRound);
    }

    [Fact]
    public void A_declined_direct_request_reopens_for_rotation_as_inspection()
    {
        var b = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), "plumbing", "Plumbing", "Ibrahim", "leak", "Ikate", null, null, null,
            TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Variable, null, 0m, TestData.Now,
            mediaKeys: new List<string> { "photo-1" });
        Assert.Equal(BookingStatus.Pending, b.Status);
        b.Reject();
        b.Rebroadcast();
        Assert.Equal(BookingStatus.Open, b.Status);
        Assert.Null(b.ArtisanId);
        Assert.Equal(AssessmentMode.Inspection, b.Assessment);
        Assert.Single(b.MediaKeys);
    }

    [Fact]
    public void Settings_carry_the_two_windows_with_sane_bounds()
    {
        var s = PlatformSettings.Default(TestData.Now);
        Assert.Equal(10, s.OfferWindowMinutes);
        Assert.Equal(60, s.DirectOfferWindowMinutes);
        s.Update(0m, 0m, 48, 1000, 500, 2000, TestData.Now, offerWindowMinutes: 5, directOfferWindowMinutes: 120);
        Assert.Equal(5, s.OfferWindowMinutes);
        Assert.Throws<ArgumentException>(() => s.Update(0m, 0m, 48, 1000, 500, 2000, TestData.Now, offerWindowMinutes: 0));
    }
}
