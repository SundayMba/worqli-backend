using Servika.Domain.Bookings;
using Servika.UnitTests.Assignment;

namespace Servika.UnitTests.Pricing;

public class OpenBiddingTests
{
    private static readonly Guid Artisan = Guid.NewGuid();

    private static Booking OpenJob() =>
        Booking.Create(Guid.NewGuid(), null, "plumbing", "Plumbing", null, "Leak under the sink", "Lekki", null, null, null,
            TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Variable, null, 0m, TestData.Now);

    [Fact]
    public void A_visit_first_bid_has_no_price_only_its_visit_fee()
    {
        var bid = Bid.PlaceVisit(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", 3000, "Need to see the pipe", TestData.Now);
        Assert.Equal(BidKind.VisitFirst, bid.Kind);
        Assert.Equal(3000, bid.VisitFeeNaira);
        Assert.Equal(0, bid.WorkmanshipNaira);
        Assert.Throws<ArgumentException>(() => Bid.PlaceVisit(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", -1, null, TestData.Now));
        Assert.Throws<ArgumentException>(() => Bid.PlaceVisit(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", Bid.MaxVisitFeeNaira + 1, null, TestData.Now));
        Assert.Throws<InvalidBookingStateException>(() => bid.Counter(1000, null, TestData.Now));
    }

    [Fact]
    public void A_free_visit_bid_can_be_made()
    {
        var bid = Bid.PlaceVisit(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", 0, null, TestData.Now);
        Assert.Equal(0, bid.AmountNaira);
    }

    [Fact]
    public void After_the_visit_the_bid_becomes_a_price()
    {
        var bid = Bid.PlaceVisit(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", 3000, null, TestData.Now);
        bid.Revise(25000, new List<BidMaterialLine> { BidMaterialLine.Create("Pipe", 2, 2500) }, null, TestData.Now);
        Assert.Equal(BidKind.Price, bid.Kind);
        Assert.Equal(0, bid.VisitFeeNaira);
        Assert.Equal(30000, bid.AmountNaira);
    }

    [Fact]
    public void Picking_a_paid_visit_assigns_the_artisan_and_waits_for_the_fee()
    {
        var b = OpenJob();
        b.AcceptVisitBid(Artisan, "Emeka", 3000, TestData.Now);
        Assert.Equal(BookingStatus.Accepted, b.Status);
        Assert.Equal(Artisan, b.ArtisanId);
        Assert.True(b.IsCustomQuote);
        Assert.True(b.AwaitingUpfrontPayment);
        Assert.Throws<InvalidBookingStateException>(() => b.StartTrip());
        b.MarkPaid(3000);
        b.StartTrip();
        b.Arrive();
        // The price after the visit credits the fee.
        b.AcceptBid(Artisan, "Emeka", 30000, TestData.Now, 25000, 5000);
        Assert.Equal(27000, b.AmountDueNaira);
    }

    [Fact]
    public void Picking_a_free_visit_is_an_inspection_with_a_quote_later()
    {
        var b = OpenJob();
        b.AcceptVisitBid(Artisan, "Emeka", 0, TestData.Now);
        Assert.False(b.PaysUpfront);
        Assert.Null(b.InitialQuoteAmountNaira);
        b.StartTrip();
        b.Arrive();
        b.AcceptBid(Artisan, "Emeka", 18000, TestData.Now, 18000, 0);
        Assert.Equal(18000, b.InitialQuoteAmountNaira);
    }

    [Fact]
    public void A_price_bid_allows_one_counter()
    {
        var bid = Bid.Place(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", 20000, new List<BidMaterialLine>(), null, TestData.Now);
        bid.Counter(17000, "Can you do 17k?", TestData.Now);
        bid.DeclineCounter(TestData.Now);
        Assert.Throws<InvalidBookingStateException>(() => bid.Counter(16000, null, TestData.Now));
    }
}
