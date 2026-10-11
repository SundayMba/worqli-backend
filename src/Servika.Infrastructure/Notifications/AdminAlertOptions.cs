namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Out-of-app admin alerts. Bound from the <c>AdminAlerts</c> section
/// (env <c>AdminAlerts__TelegramBotToken</c>, <c>AdminAlerts__TelegramChatId</c>,
/// <c>AdminAlerts__Email</c>). With nothing set the alerts are only logged.
/// </summary>
public sealed class AdminAlertOptions
{
    public const string SectionName = "AdminAlerts";

    public string TelegramBotToken { get; set; } = string.Empty;
    public string TelegramChatId { get; set; } = string.Empty;
    /// <summary>Ops inbox: gets money alerts, the daily summary, and anything Telegram failed to deliver.</summary>
    public string Email { get; set; } = string.Empty;
    public string ConsoleUrl { get; set; } = "https://admin.servika.com.ng";
    /// <summary>Hour (Africa/Lagos, 0–23) the morning summary goes out.</summary>
    public int DigestHourLagos { get; set; } = 8;
    public int SweepMinutes { get; set; } = 60;
    /// <summary>True: every alert goes to email as well as Telegram. False: email only gets
    /// money alerts, the daily summary and anything Telegram failed to deliver.</summary>
    public bool EmailEverything { get; set; } = true;

    public bool TelegramConfigured => !string.IsNullOrWhiteSpace(TelegramBotToken) && !string.IsNullOrWhiteSpace(TelegramChatId);
    public bool EmailConfigured => !string.IsNullOrWhiteSpace(Email);
}
