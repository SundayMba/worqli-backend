using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Notifications;
using Servika.Application.Referrals;

namespace Servika.Application.Bookings;

/// <summary>
/// Auto-confirms jobs the customer never got around to confirming. A job that has
/// been AwaitingConfirmation (artisan submitted proof) for longer than the
/// admin-configured window (<c>PlatformSettings.AutoConfirmHours</c>) is completed
/// automatically — so an unresponsive customer can't strand the artisan, while the
/// required proof photos keep it fair. Run periodically by a background worker.
/// </summary>
public sealed class AutoConfirmCompletionsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly NotificationEmitter _notifications;
    private readonly ReferralService _referrals;
    private readonly Payments.CashCommissionService _cashCommission;
    private readonly Payments.EscrowReleaseService _escrow;
    private readonly IPlatformSettingsRepository _settings;
    private readonly IClock _clock;

    public AutoConfirmCompletionsHandler(
        IBookingRepository bookings,
        NotificationEmitter notifications,
        ReferralService referrals,
        Payments.CashCommissionService cashCommission,
        Payments.EscrowReleaseService escrow,
        IPlatformSettingsRepository settings,
        IClock clock)
    {
        _bookings = bookings;
        _notifications = notifications;
        _referrals = referrals;
        _cashCommission = cashCommission;
        _escrow = escrow;
        _settings = settings;
        _clock = clock;
    }

    /// <summary>Completes all overdue AwaitingConfirmation bookings; returns the count.</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var settings = await _settings.GetOrCreateAsync(ct);
        var cutoff = now.AddHours(-settings.AutoConfirmHours);
        var due = await _bookings.ListAwaitingConfirmationBeforeAsync(cutoff, ct);
        if (due.Count == 0) return 0;

        foreach (var booking in due)
        {
            booking.ConfirmCompletion(now);
            _notifications.BookingAutoConfirmed(booking);
            await _notifications.ArtisanJobConfirmed(booking, ct);
            await _escrow.ReleaseIfPaidAsync(booking, ct);
            await _cashCommission.RecordIfCashJobAsync(booking, ct);
            await _referrals.AwardIfReferredAsync(booking, now, ct);
        }

        await _bookings.SaveChangesAsync(ct);
        return due.Count;
    }
}
