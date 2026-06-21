using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;
using Servika.Domain.Users;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Production <see cref="IOtpSender"/>: delivers one-time codes by email through
/// Resend (https://resend.com). Implements the same port as the dev logger, so no
/// use-case code changes — DI picks this when a Resend API key is configured.
/// Today every OTP destination is an email address (phone verification is later).
/// </summary>
public sealed class ResendEmailSender : IOtpSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        IHttpClientFactory httpClientFactory,
        ResendOptions options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(string destination, string code, OtpPurpose purpose, CancellationToken ct)
    {
        var (subject, html) = BuildEmail(code, purpose);

        var client = _httpClientFactory.CreateClient("resend");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            from = $"{_options.FromName} <{_options.FromEmail}>",
            to = new[] { destination },
            subject,
            html,
        };

        using var response = await client.PostAsJsonAsync(
            "https://api.resend.com/emails", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Resend email send failed ({Status}): {Body}",
                (int)response.StatusCode, body);
            throw new InvalidOperationException(
                "We couldn't send your verification email. Please try again.");
        }

        _logger.LogInformation("Sent {Purpose} email to {Destination} via Resend", purpose, destination);
    }

    private (string Subject, string Html) BuildEmail(string code, OtpPurpose purpose)
    {
        var (subject, heading, intro) = purpose == OtpPurpose.PasswordReset
            ? ("Reset your Servika password",
               "Reset your password",
               "Use this code to reset your Servika password. It expires in 30 minutes.")
            : ("Verify your Servika email",
               "Welcome to Servika 👋",
               "Use this code to verify your email and finish creating your account. It expires in 10 minutes.");

        var html = $"""
            <div style="font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#0F172A">
              <h2 style="margin:0 0 8px;font-size:20px">{heading}</h2>
              <p style="margin:0 0 24px;font-size:14px;line-height:20px;color:#64748B">{intro}</p>
              <div style="font-size:34px;font-weight:700;letter-spacing:8px;text-align:center;background:#FFF7ED;color:#EA580C;border-radius:14px;padding:18px 0">{code}</div>
              <p style="margin:24px 0 0;font-size:12px;line-height:18px;color:#94A3B8">If you didn't request this, you can safely ignore this email.</p>
            </div>
            """;

        return (subject, html);
    }
}
