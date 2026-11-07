using System.Security.Cryptography;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>Random 4-digit door codes. Never "0000"-style repeats, which people guess first.</summary>
public static class DoorCodes
{
    public static string New()
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");
            if (code.Distinct().Count() > 1) return code;
        }
    }
}

/// <summary>
/// The artisan types the code the customer read out at the door. Right: they may go in, and the
/// customer's screen turns green. Wrong: a try is counted (400 with the tries left). Fifth wrong try:
/// the code locks (409), the customer is told and ops are alerted.
/// </summary>
public sealed class ConfirmDoorCodeHandler
{
    private readonly ICatalogueRepository _catalogue;
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly ArtisanBookingDtoBuilder _dto;
    private readonly IClock _clock;

    public ConfirmDoorCodeHandler(ICatalogueRepository catalogue, IBookingRepository bookings,
        NotificationEmitter notifications, ArtisanBookingDtoBuilder dto, IClock clock)
    {
        _catalogue = catalogue;
        _bookings = bookings;
        _notifications = notifications;
        _dto = dto;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(Guid artisanUserId, Guid bookingId, string? code, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("This job was not found.");
        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException("This job was not found.");
        if (booking.DoorCodeLocked && booking.DoorCodePending)
            throw new ConflictException("Too many wrong codes. Ask the customer to show a new code in their app.");

        var wasPending = booking.DoorCodePending;
        var outcome = booking.ConfirmDoorCode(code ?? "", _clock.UtcNow);
        switch (outcome)
        {
            case DoorCodeOutcome.Confirmed:
                if (wasPending) _notifications.DoorCodeConfirmed(booking);
                await _bookings.SaveChangesAsync(ct);
                return await _dto.BuildAsync(booking, 0, ct);
            case DoorCodeOutcome.Locked:
                _notifications.DoorCodeLocked(booking);
                await _bookings.SaveChangesAsync(ct);
                throw new ConflictException("Too many wrong codes. Ask the customer to show a new code in their app.");
            default:
                await _bookings.SaveChangesAsync(ct);
                var left = booking.DoorCodeAttemptsLeft;
                throw new ArgumentException($"That code is wrong. {left} {(left == 1 ? "try" : "tries")} left.");
        }
    }
}

/// <summary>The customer shows a new door code (after a lock, or because they want to).</summary>
public sealed class ReissueDoorCodeHandler
{
    private readonly IBookingRepository _bookings;
    private readonly GetBookingByIdHandler _detail;

    public ReissueDoorCodeHandler(IBookingRepository bookings, GetBookingByIdHandler detail)
    {
        _bookings = bookings;
        _detail = detail;
    }

    public async Task<BookingDetailDto> HandleAsync(Guid customerId, Guid bookingId, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException("This booking was not found.");
        booking.ReissueDoorCode(DoorCodes.New());
        await _bookings.SaveChangesAsync(ct);
        return await _detail.HandleAsync(customerId, bookingId, ct);
    }
}
