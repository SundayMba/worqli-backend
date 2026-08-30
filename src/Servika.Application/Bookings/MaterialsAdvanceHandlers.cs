using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;
using Servika.Domain.Payments;

namespace Servika.Application.Bookings;

/// <summary>
/// The assigned artisan asks for part of the agreed MATERIALS money to be released
/// from the paid escrow before the job is done, so they can buy the parts. Nothing
/// moves here — the customer must approve (<see cref="DecideMaterialsAdvanceHandler"/>).
/// </summary>
public sealed class RequestMaterialsAdvanceHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public RequestMaterialsAdvanceHandler(
        IBookingRepository bookings, ICatalogueRepository catalogue,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid artisanUserId, Guid bookingId, RequestMaterialsAdvanceRequest request, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Job was not found.");
        var booking = await _bookings.FindForArtisanAsync(bookingId, profile.Id, ct)
            ?? throw new NotFoundException("Job was not found.");

        booking.RequestMaterialsAdvance(request.AmountNaira, _clock.UtcNow);
        _notifications.MaterialsAdvanceRequested(booking, request.AmountNaira);
        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto();
    }
}

/// <summary>
/// The customer approves or declines the artisan's materials request. Approval
/// writes a <see cref="WalletTransactionType.MaterialsAdvance"/> credit to the
/// artisan's ledger immediately (withdrawable now); the escrow release at completion
/// then credits the earning MINUS this advance, so the booking still nets exactly
/// once. The advance is excluded from later refunds — the customer approved it and
/// the materials exist; disputes over it go to the admin.
/// </summary>
public sealed class DecideMaterialsAdvanceHandler
{
    private readonly IBookingRepository _bookings;
    private readonly IPaymentRepository _payments;
    private readonly IWalletRepository _wallet;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public DecideMaterialsAdvanceHandler(
        IBookingRepository bookings, IPaymentRepository payments, IWalletRepository wallet,
        NotificationEmitter notifications, IClock clock)
    {
        _bookings = bookings;
        _payments = payments;
        _wallet = wallet;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, Guid bookingId, bool approve, CancellationToken ct)
    {
        var booking = await _bookings.FindForCustomerAsync(bookingId, customerId, ct)
            ?? throw new NotFoundException($"Booking '{bookingId}' was not found.");

        var now = _clock.UtcNow;
        var amount = booking.MaterialsAdvanceNaira ?? 0;

        if (approve)
        {
            var payment = await _payments.FindSucceededForBookingAsync(booking.Id, ct)
                ?? throw new ConflictException("The escrow payment for this booking was not found.");
            if (payment.IsEarningReleased)
                throw new ConflictException("This job's payment has already been released in full.");
            if (payment.ArtisanId is not { } artisanId)
                throw new ConflictException("This booking has no assigned artisan to pay.");

            booking.ApproveMaterialsAdvance(now);
            _wallet.Add(WalletTransaction.Create(
                WalletOwnerType.Artisan, artisanId,
                WalletTransactionType.MaterialsAdvance, amount,
                booking.Id, payment.Id,
                $"Materials advance for booking {booking.Id}", now));
        }
        else
        {
            booking.DeclineMaterialsAdvance(now);
        }

        await _notifications.MaterialsAdvanceDecided(booking, approve, amount, ct);
        await _bookings.SaveChangesAsync(ct);
        return booking.ToDetailDto();
    }
}
