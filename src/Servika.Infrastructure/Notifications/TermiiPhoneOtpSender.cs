using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Sends phone-verification codes via Termii (Nigeria). Delivers our own code as a
/// message: <b>WhatsApp first</b> (channel "whatsapp" — universal + cheaper here),
/// falling back to <b>SMS on the transactional "dnd" route</b> (bypasses the DND
/// registry so OTPs still land). Selected by DI when <c>Sms:ApiKey</c> is set; the
/// dev stub logs codes otherwise. Throws only if every channel fails, so the caller
/// never commits a code it couldn't deliver.
/// </summary>
public sealed class TermiiPhoneOtpSender : IPhoneOtpSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SmsOptions _options;
    private readonly ILogger<TermiiPhoneOtpSender> _logger;

    public TermiiPhoneOtpSender(
        IHttpClientFactory httpClientFactory,
        SmsOptions options,
        ILogger<TermiiPhoneOtpSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task SendAsync(string phoneNumber, string code, CancellationToken ct)
    {
        var to = NormalizeNg(phoneNumber);
        var message = $"Your Servika verification code is {code}. It expires in 10 minutes. Never share it.";

        // WhatsApp first (if enabled), then SMS over the transactional route.
        var channels = _options.WhatsAppFirst
            ? new[] { "whatsapp", "dnd" }
            : new[] { "dnd" };

        foreach (var channel in channels)
        {
            if (await TrySendAsync(to, message, channel, ct))
                return;
            _logger.LogWarning("Termii send via {Channel} failed for {Phone}; trying next channel.", channel, to);
        }

        throw new InvalidOperationException("Could not deliver the phone verification code.");
    }

    private async Task<bool> TrySendAsync(string to, string message, string channel, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("termii");
            var payload = JsonSerializer.Serialize(new
            {
                to,
                from = _options.SenderId,
                sms = message,
                type = "plain",
                channel,
                api_key = _options.ApiKey,
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(
                $"{_options.BaseUrl.TrimEnd('/')}/api/sms/send", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Termii {Channel} HTTP {Status}: {Body}", channel, response.StatusCode, body);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Termii {Channel} threw for {Phone}.", channel, to);
            return false;
        }
    }

    /// <summary>Best-effort Nigerian E.164 (digits, no '+'): local 0xxxxxxxxxx → 234xxxxxxxxxx.</summary>
    private static string NormalizeNg(string phone)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.StartsWith("0") && digits.Length == 11) return "234" + digits[1..];
        return digits;
    }
}
