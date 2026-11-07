using Servika.Application.Bookings;
using Servika.Domain.Bookings;
using Servika.UnitTests.Assignment;

namespace Servika.UnitTests.Bookings;

public class DoorCodeTests
{
    private static Booking Arrived(string? code = "4827")
    {
        var b = Booking.Create(Guid.NewGuid(), Guid.NewGuid(), "electrical", "Electrical", "Kama Mba", "Socket sparks",
            "12 Admiralty Way, Lekki Phase 1", 6.4474, 3.47, null, TestData.Now.AddDays(1), "Morning",
            Urgency.Standard, PricingModel.Variable, null, 0m, TestData.Now);
        b.Accept(TestData.Now);
        b.StartTrip(TestData.Now);
        b.Arrive(TestData.Now, code);
        return b;
    }

    [Fact]
    public void The_right_code_lets_the_artisan_in()
    {
        var b = Arrived();
        Assert.True(b.DoorCodePending);
        Assert.Equal(DoorCodeOutcome.Confirmed, b.ConfirmDoorCode(" 4827 ", TestData.Now));
        Assert.False(b.DoorCodePending);
        Assert.NotNull(b.DoorCodeConfirmedAtUtc);
        b.EnsureDoorCodeConfirmed();
    }

    [Fact]
    public void Five_wrong_codes_lock_it_until_the_customer_shows_a_new_one()
    {
        var b = Arrived();
        for (var i = 1; i < Booking.MaxDoorCodeAttempts; i++)
            Assert.Equal(DoorCodeOutcome.Wrong, b.ConfirmDoorCode("1111", TestData.Now));
        Assert.Equal(1, b.DoorCodeAttemptsLeft);
        Assert.Equal(DoorCodeOutcome.Locked, b.ConfirmDoorCode("1111", TestData.Now));
        Assert.Equal(DoorCodeOutcome.Locked, b.ConfirmDoorCode("4827", TestData.Now));   // even the right one, once locked
        b.ReissueDoorCode("5093");
        Assert.False(b.DoorCodeLocked);
        Assert.Equal(DoorCodeOutcome.Confirmed, b.ConfirmDoorCode("5093", TestData.Now));
    }

    [Fact]
    public void Work_cannot_start_before_the_code_is_entered()
    {
        var b = Arrived();
        var ex = Assert.Throws<InvalidBookingStateException>(() => b.StartWork(TestData.Now));
        Assert.Contains("door code", ex.Message);
    }

    [Fact]
    public void Jobs_that_arrived_before_door_codes_existed_are_not_held_up()
    {
        var b = Arrived(code: null);
        Assert.False(b.DoorCodePending);
        b.EnsureDoorCodeConfirmed();
    }

    [Fact]
    public void A_confirmed_code_cannot_be_replaced()
    {
        var b = Arrived();
        b.ConfirmDoorCode("4827", TestData.Now);
        Assert.Throws<InvalidBookingStateException>(() => b.ReissueDoorCode("1234"));
    }

    [Fact]
    public void New_codes_are_four_digits_and_never_all_the_same()
    {
        for (var i = 0; i < 500; i++)
        {
            var c = DoorCodes.New();
            Assert.Equal(4, c.Length);
            Assert.True(c.All(char.IsAsciiDigit));
            Assert.True(c.Distinct().Count() > 1);
        }
    }
}
