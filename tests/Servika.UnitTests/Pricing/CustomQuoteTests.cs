using Servika.Application.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Settings;
using Servika.UnitTests.Assignment;

namespace Servika.UnitTests.Pricing;

public class CustomQuoteTests
{
    private const int Fee = 5000;
    private static readonly Guid Artisan = Guid.NewGuid();

    /// <summary>A custom job ("new split AC installation", typical labour ₦25,000 to ₦60,000), visit paid, artisan there.</summary>
    private static Booking AtTheDoor(int? rangeMax = 60000)
    {
        var b = Booking.Create(Guid.NewGuid(), Artisan, "ac", "New split AC installation", "Emeka", "Fit a 1.5HP", "Lekki",
            null, null, null, TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Hybrid, Fee, 0m, TestData.Now,
            diagnosisFeeNaira: Fee, customQuote: true, customPriceBookItemId: Guid.NewGuid(),
            typicalRangeMinNaira: rangeMax is null ? null : 25000, typicalRangeMaxNaira: rangeMax);
        b.MarkPaid(Fee);
        b.Accept(TestData.Now);
        b.StartTrip();
        b.Arrive();
        return b;
    }

    private static Bid Quote(int labour, int materials = 0) =>
        Bid.Place(Guid.NewGuid(), Artisan, Guid.NewGuid(), "Emeka", labour,
            materials > 0 ? new List<BidMaterialLine> { BidMaterialLine.Create("Copper piping", 1, materials) } : new List<BidMaterialLine>(),
            null, TestData.Now);

    [Fact]
    public void A_quote_within_the_lines_goes_straight_to_the_customer()
    {
        var reasons = QuoteReviewRules.Reasons(AtTheDoor(), Quote(40000, 8000), PlatformSettings.Default(TestData.Now));
        Assert.Empty(reasons);
    }

    [Fact]
    public void A_large_total_or_labour_far_above_the_range_waits_for_ops()
    {
        var s = PlatformSettings.Default(TestData.Now);
        Assert.Single(QuoteReviewRules.Reasons(AtTheDoor(), Quote(45000, 10000), s));      // ₦55,000 ≥ ₦50,000
        Assert.Equal(2, QuoteReviewRules.Reasons(AtTheDoor(), Quote(95000), s).Count);     // > 1.5 × ₦60,000 and ≥ ₦50,000
        Assert.Empty(QuoteReviewRules.Reasons(AtTheDoor(rangeMax: null), Quote(45000), s)); // no range: threshold only
    }

    [Fact]
    public void Ops_pass_or_send_back_and_the_customer_only_sees_a_passed_quote()
    {
        var bid = Quote(95000);
        bid.SetReviewNeeded(true);
        Assert.False(bid.IsVisibleToCustomer);
        Assert.Throws<ArgumentException>(() => bid.SendBackForChanges(Guid.NewGuid(), "too high", TestData.Now));
        bid.SendBackForChanges(Guid.NewGuid(), "Labour is far above what installs cost in Lekki.", TestData.Now);
        Assert.False(bid.IsVisibleToCustomer);
        bid.Revise(55000, new List<BidMaterialLine>(), null, TestData.Now);
        bid.SetReviewNeeded(true);
        bid.ApproveReview(Guid.NewGuid(), TestData.Now);
        Assert.True(bid.IsVisibleToCustomer);
        Assert.Null(bid.ReviewNote);
    }

    [Fact]
    public void Accepting_a_quote_credits_the_visit_and_work_waits_for_the_rest()
    {
        var b = AtTheDoor();
        Assert.Throws<InvalidBookingStateException>(() => b.StartWork(TestData.Now));
        b.AcceptBid(Artisan, "Emeka", 48000, TestData.Now, 40000, 8000);
        Assert.Equal(48000, b.InitialQuoteAmountNaira);
        Assert.Equal(43000, b.AmountDueNaira);
        Assert.True(b.HasAcceptedCustomQuote);
        Assert.Throws<InvalidBookingStateException>(() => b.StartWork(TestData.Now));
        Assert.Throws<InvalidBookingStateException>(() => b.AcceptBid(Artisan, "Emeka", 30000, TestData.Now, 30000, 0));
        b.MarkPaid(43000);
        b.StartWork(TestData.Now);
        Assert.Equal(BookingStatus.InProgress, b.Status);
    }

    [Fact]
    public void Declining_ends_the_visit_but_not_after_a_quote_was_accepted()
    {
        var declined = AtTheDoor();
        declined.CompleteAsDiagnosisOnly(TestData.Now);
        Assert.Equal(BookingStatus.Completed, declined.Status);

        var accepted = AtTheDoor();
        accepted.AcceptBid(Artisan, "Emeka", 48000, TestData.Now, 40000, 8000);
        Assert.Throws<InvalidBookingStateException>(() => accepted.CompleteAsDiagnosisOnly(TestData.Now));
    }
}
