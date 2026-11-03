using Servika.Application.Abstractions.Persistence;
using Servika.Application.Catalogue;
using Servika.Domain.Bookings;
using Servika.Domain.Catalogue;
using Servika.Domain.Payments;
using Servika.Domain.Settings;
using Servika.UnitTests.Assignment;

namespace Servika.UnitTests.Pricing;

public class PriceBookItemTests
{
    private static PriceBookItem Fixed(int? price = 15000, bool popular = true) =>
        PriceBookItem.Create("AC", PriceBookItemKind.Fixed, " AC servicing ", "Wash coils", price, null, null, null, 90,
            popular, true, 1, true, null, TestData.Now);

    [Fact]
    public void A_fixed_job_needs_a_price_and_is_cleaned_up()
    {
        var item = Fixed();
        Assert.Equal("ac", item.CategorySlug);
        Assert.Equal("AC servicing", item.Name);
        Assert.Equal(15000, item.PriceNaira);
        Assert.Throws<ArgumentException>(() => Fixed(price: null));
        Assert.Throws<ArgumentException>(() => Fixed(price: 0));
        Assert.Throws<ArgumentException>(() => Fixed(price: PriceBookItem.MaxPriceNaira + 1));
    }

    [Fact]
    public void A_custom_item_carries_a_range_and_never_a_price()
    {
        var item = PriceBookItem.Create("ac", PriceBookItemKind.Custom, "New AC installation", "", 99000, 25000, 60000,
            null, null, true, true, 1, true, null, TestData.Now);
        Assert.Null(item.PriceNaira);
        Assert.False(item.IsPopular); // only whole jobs are sold on Home
        Assert.True(item.IsAboveRange(95000, 1.5m));
        Assert.False(item.IsAboveRange(90000, 1.5m));
        Assert.Throws<ArgumentException>(() => PriceBookItem.Create("ac", PriceBookItemKind.Custom, "x", "", null, 60000,
            25000, null, null, false, true, 1, true, null, TestData.Now));
        Assert.Throws<ArgumentException>(() => PriceBookItem.Create("ac", PriceBookItemKind.Custom, "x", "", null, null,
            null, null, null, false, true, 1, true, null, TestData.Now));
    }

    [Fact]
    public void Editing_records_who_and_when()
    {
        var item = Fixed();
        var admin = Guid.NewGuid();
        item.Update("ac", PriceBookItemKind.Fixed, "AC servicing", "Wash coils", 17000, null, null, null, 90, true, true, 1,
            false, admin, TestData.Now.AddDays(1));
        Assert.Equal(17000, item.PriceNaira);
        Assert.False(item.IsPlaceholderPrice);
        Assert.Equal(admin, item.UpdatedByUserId);
        Assert.Equal(TestData.Now.AddDays(1), item.UpdatedAtUtc);
    }
}

public class UpfrontBookingTests
{
    private static Booking PriceBookJob(Guid? artisanId = null) =>
        Booking.Create(Guid.NewGuid(), artisanId, "ac", "AC servicing", artisanId is null ? null : "Emeka", "Wash coils",
            "Lekki", null, null, null, TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Fixed, 15000,
            0m, TestData.Now, priceBookItemId: Guid.NewGuid());

    [Fact]
    public void A_price_book_job_waits_for_payment_then_stops_waiting()
    {
        var b = PriceBookJob();
        Assert.True(b.PaysUpfront);
        Assert.True(b.AwaitingUpfrontPayment);
        b.MarkPaid();
        Assert.False(b.AwaitingUpfrontPayment);
    }

    [Fact]
    public void A_price_book_booking_must_carry_its_fixed_price()
    {
        Assert.Throws<ArgumentException>(() => Booking.Create(Guid.NewGuid(), null, "ac", "AC servicing", null, "x",
            "Lekki", null, null, null, TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Variable, null,
            0m, TestData.Now, priceBookItemId: Guid.NewGuid()));
    }

    [Fact]
    public void Only_an_unpaid_upfront_booking_lapses()
    {
        var unpaid = PriceBookJob(Guid.NewGuid());
        unpaid.ExpireUnpaid();
        Assert.Equal(BookingStatus.Expired, unpaid.Status);

        var paid = PriceBookJob();
        paid.MarkPaid();
        Assert.Throws<InvalidBookingStateException>(() => paid.ExpireUnpaid());

        var quoted = TestData.OpenJob();
        Assert.Throws<InvalidBookingStateException>(() => quoted.ExpireUnpaid());
    }

    [Fact]
    public void An_upfront_payment_is_attributed_to_the_artisan_who_did_the_job()
    {
        var p = Payment.Initiate(Guid.NewGuid(), Guid.NewGuid(), null, 15000, 0m, "stub", "ref", null, TestData.Now);
        var artisan = Guid.NewGuid();
        p.AttributeTo(artisan);
        Assert.Equal(artisan, p.ArtisanId);
    }

    [Fact]
    public void Pricing_levers_are_range_guarded()
    {
        var s = PlatformSettings.Default(TestData.Now);
        Assert.Equal(5000, s.DiagnosisFeeNaira);
        s.UpdatePricing(7000, 60000, 2m, TestData.Now);
        Assert.Equal(7000, s.DiagnosisFeeNaira);
        Assert.Throws<ArgumentException>(() => s.UpdatePricing(-1, 60000, 2m, TestData.Now));
        Assert.Throws<ArgumentException>(() => s.UpdatePricing(5000, 60000, 0.5m, TestData.Now));
    }
}

public class PriceSignalsTests
{
    private static readonly Guid Item = Guid.NewGuid();

    private static List<PriceBookBookingFact> Bookings(int paid, int unpaid = 0, int ranOut = 0) =>
        Enumerable.Range(0, paid).Select(i => new PriceBookBookingFact(Item, BookingStatus.Completed,
                BookingPaymentState.Paid, i < ranOut ? TestData.Now : null))
            .Concat(Enumerable.Range(0, unpaid).Select(_ => new PriceBookBookingFact(Item, BookingStatus.Expired,
                BookingPaymentState.Unpaid, null)))
            .ToList();

    private static List<PriceBookOfferFact> Offers(int accepted, int passed = 0, string reason = "Busy then") =>
        Enumerable.Range(0, accepted).Select(_ => new PriceBookOfferFact(Item, JobOfferStatus.Accepted, null))
            .Concat(Enumerable.Range(0, passed).Select(_ => new PriceBookOfferFact(Item, JobOfferStatus.Declined, reason)))
            .ToList();

    [Fact]
    public void Too_few_jobs_is_not_judged()
    {
        var s = PriceSignals.Compute(Bookings(2), Offers(2));
        Assert.Equal("TooFewJobs", s.Verdict);
    }

    [Fact]
    public void Artisans_taking_it_and_customers_paying_looks_right()
    {
        var s = PriceSignals.Compute(Bookings(10), Offers(10, passed: 1));
        Assert.Equal("LooksRight", s.Verdict);
        Assert.Equal(10, s.Completed);
    }

    [Fact]
    public void Artisans_passing_on_price_says_too_low()
    {
        var s = PriceSignals.Compute(Bookings(8), Offers(8, passed: 3, reason: PriceSignals.PriceTooLowReason));
        Assert.Equal("MaybeTooLow", s.Verdict);
        Assert.Contains("Price too low (3)", s.TopPassReasons);
    }

    [Fact]
    public void Customers_stopping_at_payment_says_too_high()
    {
        var s = PriceSignals.Compute(Bookings(5, unpaid: 5), Offers(5));
        Assert.Equal("MaybeTooHigh", s.Verdict);
        Assert.Equal(5, s.LeftUnpaid);
    }

    [Fact]
    public void Rotation_running_out_says_too_low()
    {
        var s = PriceSignals.Compute(Bookings(8, ranOut: 3), Offers(8));
        Assert.Equal("MaybeTooLow", s.Verdict);
    }
}
