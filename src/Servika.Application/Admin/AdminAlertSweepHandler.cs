using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Domain.Catalogue;
using Servika.Domain.Disputes;

namespace Servika.Application.Admin;

/// <summary>
/// The timed admin alerts: a one-off nudge when an application has waited past
/// the review target, and the daily summary (queue, disputes, refunds, float).
/// Called by a background sweep; stateless, so each run only looks at the
/// window since the previous one.
/// </summary>
public sealed class AdminAlertSweepHandler
{
    public const int OverdueHours = 24;

    private readonly IArtisanKycRepository _kyc;
    private readonly IUserRepository _users;
    private readonly ICatalogueRepository _catalogue;
    private readonly IDisputeRepository _disputes;
    private readonly GetAdminPaymentsHandler _payments;
    private readonly IAdminAlerts _alerts;
    private readonly IClock _clock;

    public AdminAlertSweepHandler(
        IArtisanKycRepository kyc, IUserRepository users, ICatalogueRepository catalogue,
        IDisputeRepository disputes, GetAdminPaymentsHandler payments, IAdminAlerts alerts, IClock clock)
    {
        _kyc = kyc;
        _users = users;
        _catalogue = catalogue;
        _disputes = disputes;
        _payments = payments;
        _alerts = alerts;
        _clock = clock;
    }

    /// <summary>Nudge once for every application that crossed the review target
    /// within the last <paramref name="window"/> (the sweep interval).</summary>
    public async Task<int> SendOverdueAsync(TimeSpan window, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var target = TimeSpan.FromHours(OverdueHours);
        var pending = await _kyc.ListAsync(ArtisanVerificationStatus.Pending, ct);
        var sent = 0;
        foreach (var k in pending.Where(k => k.OpenCheck is null))
        {
            var waited = now - (k.ResubmittedAtUtc ?? k.SubmittedAtUtc);
            if (waited < target || waited >= target + window) continue;
            var (name, trade) = await DescribeArtisanAsync(_catalogue, _users, k.UserId, ct);
            _alerts.Send(new AdminAlert(AdminAlertKind.ApplicationOverdue,
                "Application waiting over a day",
                new[] { $"{name}{trade}", $"Waiting {(int)waited.TotalHours} hours. Approve, decline or ask for a change." },
                $"/verifications/{k.Id}", "Review now"));
            sent++;
        }
        return sent;
    }

    /// <summary>The morning summary. Always sent, so silence means the alerts are broken.</summary>
    public async Task SendDigestAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var pending = await _kyc.ListAsync(ArtisanVerificationStatus.Pending, ct);
        var onYou = pending.Where(k => k.OpenCheck is null).ToList();
        var overdue = onYou.Count(k => now - (k.ResubmittedAtUtc ?? k.SubmittedAtUtc) >= TimeSpan.FromHours(OverdueHours));
        var onArtisan = pending.Count - onYou.Count;
        var disputes = (await _disputes.ListAllAsync(null, ct)).Count(d => d.Status != DisputeStatus.Resolved);
        var pay = await _payments.HandleAsync(ct);
        var failedRefunds = pay.RefundsNeedingAttention.Count;

        var lines = new List<string>
        {
            $"Applications waiting on you: {onYou.Count}{(overdue > 0 ? $" ({overdue} over a day)" : "")}",
            $"Waiting on the artisan: {onArtisan}",
            $"Open disputes: {disputes}",
            $"Refunds to chase: {failedRefunds}",
        };
        if (pay.FloatHealthy is false)
            lines.Add($"Paystack balance is short by ₦{pay.FloatShortfallNaira:N0}. Top up before payouts fail.");
        else if (pay.GatewayBalanceNaira is { } bal)
            lines.Add($"Paystack balance ₦{bal:N0} covers ₦{pay.LiabilitiesNaira:N0} owed.");
        if (onYou.Count == 0 && disputes == 0 && failedRefunds == 0 && pay.FloatHealthy is not false)
            lines.Add("Nothing needs you today.");

        _alerts.Send(new AdminAlert(AdminAlertKind.DailyDigest, "Servika morning summary", lines, "/", "Open console"));

        if (pay.FloatHealthy is false)
            _alerts.Send(new AdminAlert(AdminAlertKind.FloatShort, "Paystack balance is short",
                new[] { $"Short by ₦{pay.FloatShortfallNaira:N0}. Artisan withdrawals will fail until it is topped up." },
                "/payments", "Open payments"));
    }

    /// <summary>Shared by the submit handlers so new-application alerts read the same.</summary>
    public static async Task<(string Name, string Trade)> DescribeArtisanAsync(
        ICatalogueRepository catalogue, IUserRepository users, Guid userId, CancellationToken ct)
    {
        var profile = await catalogue.GetArtisanByUserIdAsync(userId, ct);
        var name = profile?.FullName;
        if (string.IsNullOrWhiteSpace(name)) name = (await users.FindByIdAsync(userId, ct))?.FullName ?? "An artisan";
        var bits = new[] { profile?.Specialty, profile?.Location }.Where(s => !string.IsNullOrWhiteSpace(s));
        var trade = string.Join(" · ", bits);
        return (name!, trade.Length > 0 ? $" · {trade}" : "");
    }
}
