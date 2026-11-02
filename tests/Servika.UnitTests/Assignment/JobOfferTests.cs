using Servika.Domain.Bookings;

namespace Servika.UnitTests.Assignment;

public class JobOfferTests
{
    private static JobOffer Offer(int minutes = 10) =>
        JobOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, JobOfferSource.Rotation, TestData.Now, TestData.Now.AddMinutes(minutes));

    [Fact]
    public void Live_until_it_expires()
    {
        var o = Offer();
        Assert.True(o.IsLive(TestData.Now.AddMinutes(9)));
        Assert.False(o.IsLive(TestData.Now.AddMinutes(10)));
    }

    [Fact]
    public void Accept_and_decline_only_while_live()
    {
        var o = Offer();
        o.Accept(TestData.Now.AddMinutes(1));
        Assert.Equal(JobOfferStatus.Accepted, o.Status);
        Assert.Throws<InvalidBookingStateException>(() => o.Decline(TestData.Now.AddMinutes(2), "busy"));

        var late = Offer();
        Assert.Throws<InvalidBookingStateException>(() => late.Accept(TestData.Now.AddMinutes(11)));
    }

    [Fact]
    public void Decline_keeps_the_reason_trimmed()
    {
        var o = Offer();
        o.Decline(TestData.Now, "  Too far  ");
        Assert.Equal(JobOfferStatus.Declined, o.Status);
        Assert.Equal("Too far", o.DeclineReason);
    }

    [Fact]
    public void Expire_and_withdraw_are_idempotent_and_never_overwrite_an_answer()
    {
        var o = Offer();
        o.Expire(TestData.Now.AddMinutes(11));
        o.Expire(TestData.Now.AddMinutes(12));
        o.Withdraw(TestData.Now.AddMinutes(13));
        Assert.Equal(JobOfferStatus.Expired, o.Status);

        var accepted = Offer();
        accepted.Accept(TestData.Now);
        accepted.Withdraw(TestData.Now);
        Assert.Equal(JobOfferStatus.Accepted, accepted.Status);
    }

    [Fact]
    public void An_offer_must_expire_in_the_future() =>
        Assert.Throws<ArgumentException>(() =>
            JobOffer.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, JobOfferSource.Direct, TestData.Now, TestData.Now));
}
