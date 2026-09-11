using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Notifications;
using Servika.Domain.Payments;
using Servika.Domain.Settings;
using Servika.Domain.Users;

namespace Servika.Application.Payments;

/// <summary>
/// Tells every customer and artisan, in advance, that transaction fees are about to
/// start (30, 7 and 1 day before) and again the day they go live. Driven by the
/// admin-set <see cref="PlatformSettings.FeesStartAtUtc"/>; each stage is sent once
/// per scheduled date (changing the date resets the stages). Run by the
/// <c>FeeNoticeService</c> sweep. Sends only the highest stage that is due, so a
/// date set five days out produces one "in 5 days" notice, not three.
/// </summary>
public sealed class SendFeeNoticesHandler
{
    private readonly IPlatformSettingsRepository _settings;
    private readonly IUserRepository _users;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;

    public SendFeeNoticesHandler(
        IPlatformSettingsRepository settings,
        IUserRepository users,
        NotificationEmitter notifications,
        IClock clock)
    {
        _settings = settings;
        _users = users;
        _notifications = notifications;
        _clock = clock;
    }

    /// <summary>Returns how many notifications were written (0 when nothing is due).</summary>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var s = await _settings.GetOrCreateAsync(ct);
        if (s.FeesStartAtUtc is not { } start) return 0;

        var now = _clock.UtcNow;
        var daysLeft = (start - now).TotalDays;
        var due = daysLeft <= 0 ? PlatformSettings.FeeNoticeLive
            : daysLeft <= 1 ? PlatformSettings.FeeNotice1Day
            : daysLeft <= 7 ? PlatformSettings.FeeNotice7Days
            : daysLeft <= 30 ? PlatformSettings.FeeNotice30Days
            : 0;
        if (due == 0 || s.FeeNoticeStage >= due) return 0;

        var users = await _users.ListAsync(null, ct);
        var count = 0;
        foreach (var u in users)
        {
            if (u.IsDeleted || u.IsSuspended) continue;
            if (u.Role is Role.Admin or Role.SuperAdmin) continue;
            _notifications.FeeNotice(u.Id, u.Role == Role.Artisan, due, start, s);
            count++;
        }

        s.MarkFeeNoticeSent(due, now);
        await _settings.SaveChangesAsync(ct);
        return count;
    }
}
