using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Notifications;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// The artisan fixed the check the reviewer flagged (guarantors, payout, trade, photo,
/// documents) and sends the application back. Identity and selfie fixes go through the
/// normal KYC upload instead, which closes the open check on its own.
/// </summary>
public sealed class ResubmitKycCheckHandler
{
    private readonly IArtisanKycRepository _kyc;
    private readonly IVerificationEventRepository _events;
    private readonly IUserRepository _users;
    private readonly NotificationEmitter _notifications;
    private readonly IClock _clock;
    private readonly ICatalogueRepository _catalogue;
    private readonly Abstractions.Notifications.IAdminAlerts _alerts;

    public ResubmitKycCheckHandler(IArtisanKycRepository kyc, IVerificationEventRepository events, IUserRepository users, NotificationEmitter notifications, IClock clock, ICatalogueRepository catalogue, Abstractions.Notifications.IAdminAlerts alerts)
    {
        _catalogue = catalogue;
        _alerts = alerts;
        _kyc = kyc;
        _events = events;
        _users = users;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<KycStatusDto> HandleAsync(Guid artisanUserId, CancellationToken ct)
    {
        var kyc = await _kyc.GetForUserAsync(artisanUserId, ct)
            ?? throw new NotFoundException("Send your identity check first.");
        if (!kyc.HasOpenCheck)
            throw new ConflictException("Nothing is waiting on you right now.");

        var now = _clock.UtcNow;
        var check = kyc.OpenCheck!.Value;
        kyc.MarkResubmitted(now);
        _events.Add(VerificationEvent.Create(kyc.Id, artisanUserId, null, VerificationEventAction.Resubmitted, check, null, null, now));

        var artisan = await _users.FindByIdAsync(artisanUserId, ct);
        await _notifications.KycResubmittedAsync(artisan?.FullName ?? "An artisan", check.ToString(), ct);

        await _kyc.SaveChangesAsync(ct);

        var (name, trade) = await Admin.AdminAlertSweepHandler.DescribeArtisanAsync(_catalogue, _users, artisanUserId, ct);
        _alerts.Send(new Abstractions.Notifications.AdminAlert(Abstractions.Notifications.AdminAlertKind.Resubmission,
            "Fix sent back for review",
            new[] { $"{name}{trade}", $"They fixed {check.ToString().ToLowerInvariant()} and are at the front of the queue." },
            $"/verifications/{kyc.Id}", "Review now"));
        return kyc.ToStatusDto((await _events.ListForKycAsync(kyc.Id, ct)).ToArtisanDtos());
    }
}
