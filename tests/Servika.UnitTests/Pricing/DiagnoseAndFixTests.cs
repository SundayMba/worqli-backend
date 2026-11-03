using Servika.Domain.Bookings;
using Servika.UnitTests.Assignment;

namespace Servika.UnitTests.Pricing;

public class DiagnoseAndFixTests
{
    private const int Fee = 5000;

    /// <summary>A diagnosis visit, paid, with the artisan at the door.</summary>
    private static Booking AtTheDoor()
    {
        var b = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), "ac", "AC Repair", "Emeka", "Not cooling", "Lekki", null, null,
            null, TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Hybrid, Fee, 0m, TestData.Now,
            diagnosisFeeNaira: Fee);
        b.MarkPaid(Fee);
        b.Accept(TestData.Now);
        b.StartTrip();
        b.Arrive();
        return b;
    }

    [Fact]
    public void A_diagnosis_visit_is_paid_upfront_at_its_fee()
    {
        var b = AtTheDoor();
        Assert.True(b.IsDiagnosis);
        Assert.True(b.PaysUpfront);
        Assert.Equal(BookingPaymentState.Paid, b.PaymentState);
        Assert.Equal(0, b.AmountDueNaira);
    }

    [Fact]
    public void Approved_repairs_credit_the_fee_the_customer_pays_the_difference()
    {
        // Business Model v1.4: capacitor ₦8,000 + gas ₦20,000 after a ₦5,000 diagnosis = ₦28,000 in total.
        var b = AtTheDoor();
        b.AddApprovedRepairs(28000);
        Assert.Equal(28000, b.InitialQuoteAmountNaira);
        Assert.Equal(23000, b.AmountDueNaira);
        Assert.Equal(BookingPaymentState.Unpaid, b.PaymentState);
        b.MarkPaid(23000);
        Assert.Equal(BookingPaymentState.Paid, b.PaymentState);
        Assert.Equal(0, b.AmountDueNaira);
    }

    [Fact]
    public void A_repair_cheaper_than_the_fee_costs_nothing_more()
    {
        var b = AtTheDoor();
        b.AddApprovedRepairs(3000);
        Assert.Equal(Fee, b.InitialQuoteAmountNaira);
        Assert.Equal(0, b.AmountDueNaira);
        Assert.Equal(BookingPaymentState.Paid, b.PaymentState);
    }

    [Fact]
    public void Work_cannot_start_until_repairs_are_approved_and_paid()
    {
        var b = AtTheDoor();
        Assert.Throws<InvalidBookingStateException>(() => b.StartWork(TestData.Now));
        b.AddApprovedRepairs(20000);
        Assert.Throws<InvalidBookingStateException>(() => b.StartWork(TestData.Now));
        b.MarkPaid(15000);
        b.StartWork(TestData.Now);
        Assert.Equal(BookingStatus.InProgress, b.Status);
    }

    [Fact]
    public void Declining_every_repair_ends_the_visit_as_the_whole_job()
    {
        var b = AtTheDoor();
        b.CompleteAsDiagnosisOnly(TestData.Now);
        Assert.Equal(BookingStatus.Completed, b.Status);

        var approved = AtTheDoor();
        approved.AddApprovedRepairs(8000);
        Assert.Throws<InvalidBookingStateException>(() => approved.CompleteAsDiagnosisOnly(TestData.Now));
    }

    [Fact]
    public void Extras_on_a_fixed_job_add_to_its_price_and_block_closing_until_paid()
    {
        var b = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), "ac", "AC servicing", "Emeka", "Wash", "Lekki", null, null,
            null, TestData.Now.AddDays(1), "Morning", Urgency.Standard, PricingModel.Fixed, 15000, 0m, TestData.Now,
            priceBookItemId: Guid.NewGuid());
        b.MarkPaid(15000);
        b.Accept(TestData.Now);
        b.StartTrip();
        b.Arrive();
        b.StartWork(TestData.Now);
        b.AddApprovedRepairs(6000);
        Assert.Equal(21000, b.InitialQuoteAmountNaira);
        Assert.Equal(6000, b.AmountDueNaira);
        Assert.Throws<InvalidBookingStateException>(() => b.SubmitCompletion(new[] { "a", "b" }, null, TestData.Now));
        b.MarkPaid(6000);
        b.SubmitCompletion(new[] { "a", "b" }, null, TestData.Now);
        Assert.Equal(BookingStatus.AwaitingConfirmation, b.Status);
    }

    [Fact]
    public void A_proposed_repair_needs_its_photo_and_a_note_for_parts()
    {
        var ok = BookingRepair.Propose(Guid.NewGuid(), Guid.NewGuid(), 1, "Replace capacitor", 8000, 4500, "35µF capacitor", "key", TestData.Now);
        Assert.Equal(12500, ok.TotalNaira);
        Assert.Throws<ArgumentException>(() => BookingRepair.Propose(Guid.NewGuid(), Guid.NewGuid(), 1, "x", 8000, 0, null, "", TestData.Now));
        Assert.Throws<ArgumentException>(() => BookingRepair.Propose(Guid.NewGuid(), Guid.NewGuid(), 1, "x", 8000, 4500, null, "key", TestData.Now));
        ok.Decide(true, TestData.Now);
        Assert.Throws<InvalidBookingStateException>(() => ok.Decide(false, TestData.Now));
    }
}
