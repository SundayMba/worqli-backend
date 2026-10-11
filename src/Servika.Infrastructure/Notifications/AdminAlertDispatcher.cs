using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Sends <see cref="AdminAlert"/>s to the admins' Telegram group (instant, on the
/// phone) and to the ops inbox by email via Resend. Email gets money alerts and
/// the daily summary every time, and any alert Telegram could not deliver. Runs on
/// a background task after a short delay so the producing transaction commits
/// first; failures are logged, never thrown.
/// </summary>
public sealed class AdminAlertDispatcher : IAdminAlerts
{
    private static readonly HashSet<AdminAlertKind> AlwaysEmail = new()
    {
        AdminAlertKind.RefundFailed, AdminAlertKind.PaymentMismatch, AdminAlertKind.FloatShort, AdminAlertKind.DailyDigest,
    };

    private readonly IHttpClientFactory _http;
    private readonly AdminAlertOptions _options;
    private readonly ResendOptions _resend;
    private readonly ILogger<AdminAlertDispatcher> _logger;

    public AdminAlertDispatcher(IHttpClientFactory http, AdminAlertOptions options, ResendOptions resend, ILogger<AdminAlertDispatcher> logger)
    {
        _http = http;
        _options = options;
        _resend = resend;
        _logger = logger;
    }

    public void Send(AdminAlert alert)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);
                var link = alert.LinkPath is null ? null : $"{_options.ConsoleUrl.TrimEnd('/')}{alert.LinkPath}";

                var telegramOk = false;
                if (_options.TelegramConfigured)
                    telegramOk = await SendTelegramAsync(alert, link);

                if (_options.EmailConfigured && _resend.IsConfigured && (_options.EmailEverything || !telegramOk || AlwaysEmail.Contains(alert.Kind)))
                    await SendEmailAsync(alert, link);

                if (!_options.TelegramConfigured && !_options.EmailConfigured)
                    _logger.LogInformation("[ADMIN-ALERT] {Title}: {Lines}", alert.Title, string.Join(" | ", alert.Lines));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Admin alert '{Title}' failed.", alert.Title);
            }
        });
    }

    private async Task<bool> SendTelegramAsync(AdminAlert alert, string? link)
    {
        var text = $"<b>{Enc(Icon(alert.Kind))} {Enc(alert.Title)}</b>\n" + string.Join("\n", alert.Lines.Select(Enc));
        // Telegram only accepts public https URLs on buttons; in dev the link goes in the text.
        var button = link is not null && link.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (link is not null && !button) text += $"\n{Enc(link)}";

        object payload = button
            ? new { chat_id = _options.TelegramChatId, text, parse_mode = "HTML", disable_web_page_preview = true,
                    reply_markup = new { inline_keyboard = new[] { new[] { new { text = alert.LinkLabel, url = link } } } } }
            : new { chat_id = _options.TelegramChatId, text, parse_mode = "HTML", disable_web_page_preview = true };

        var client = _http.CreateClient("telegram");
        using var res = await client.PostAsJsonAsync($"https://api.telegram.org/bot{_options.TelegramBotToken}/sendMessage", payload);
        if (res.IsSuccessStatusCode) return true;
        _logger.LogWarning("Telegram admin alert failed ({Status}): {Body}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
        return false;
    }

    private async Task SendEmailAsync(AdminAlert alert, string? link)
    {
        var items = string.Join("", alert.Lines.Select(l => $"<p style=\"margin:0 0 8px;font-size:14px;line-height:20px;color:#334155\">{Enc(l)}</p>"));
        var button = link is null ? "" :
            $"<a href=\"{Enc(link)}\" style=\"display:inline-block;margin-top:12px;background:#0B1220;color:#fff;text-decoration:none;font-weight:600;font-size:14px;padding:10px 16px;border-radius:10px\">{Enc(alert.LinkLabel)}</a>";
        var html = $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#0F172A">
              <div style="font-size:11px;letter-spacing:.12em;text-transform:uppercase;color:#C2560B;font-weight:600">Servika admin</div>
              <h2 style="margin:6px 0 14px;font-size:19px">{Enc(alert.Title)}</h2>
              {items}{button}
            </div>
            """;

        var client = _http.CreateClient("resend");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _resend.ApiKey);
        using var res = await client.PostAsJsonAsync("https://api.resend.com/emails", new
        {
            from = $"Servika Alerts <{_resend.FromEmail}>",
            to = new[] { _options.Email },
            subject = $"[Servika] {alert.Title}",
            html,
        });
        if (!res.IsSuccessStatusCode)
            _logger.LogWarning("Admin alert email failed ({Status}): {Body}", (int)res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private static string Icon(AdminAlertKind k) => k switch
    {
        AdminAlertKind.NewApplication => "🆕",
        AdminAlertKind.Resubmission => "🔁",
        AdminAlertKind.ApplicationOverdue => "⏰",
        AdminAlertKind.DisputeRaised or AdminAlertKind.DisputeResponse => "⚠️",
        AdminAlertKind.RefundFailed or AdminAlertKind.PaymentMismatch or AdminAlertKind.FloatShort => "🚨",
        _ => "☀️",
    };

    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}
