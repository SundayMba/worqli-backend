using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Sends push notifications via Expo's push service (https://exp.host). No API key
/// is needed to send to Expo push tokens. Best-effort: any failure is logged and
/// swallowed — a push must never break the flow that triggered it. Expo accepts up
/// to 100 messages per request, so tokens are chunked.
/// <para>
/// Two Expo rules this sender has to live with: (1) one request may only carry tokens
/// from ONE Expo project, and a user signed into both the customer app and Servika Pro
/// holds tokens from two (Expo rejects the whole request with
/// <c>PUSH_TOO_MANY_EXPERIENCE_IDS</c>, naming which token belongs where, so the batch
/// is split by that answer and resent); (2) a token whose app was uninstalled comes
/// back as a <c>DeviceNotRegistered</c> ticket and must be dropped, or Expo will
/// eventually block the project.
/// </para>
/// </summary>
public sealed class ExpoPushSender : IPushSender
{
    private const string Endpoint = "https://exp.host/--/api/v2/push/send";
    private const int ChunkSize = 100;
    private const string MixedProjectsCode = "PUSH_TOO_MANY_EXPERIENCE_IDS";
    private const string DeviceNotRegistered = "DeviceNotRegistered";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(IHttpClientFactory httpFactory, ILogger<ExpoPushSender> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> SendAsync(
        IReadOnlyCollection<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct)
    {
        var all = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
        if (all.Count == 0) return Array.Empty<string>();

        var client = _httpFactory.CreateClient("expo-push");
        var dead = new List<string>();

        for (var i = 0; i < all.Count; i += ChunkSize)
        {
            var chunk = all.Skip(i).Take(ChunkSize).ToList();
            await SendChunkAsync(client, chunk, title, body, data, dead, allowSplit: true, ct);
        }

        return dead;
    }

    private async Task SendChunkAsync(
        HttpClient client,
        IReadOnlyList<string> chunk,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        List<string> dead,
        bool allowSplit,
        CancellationToken ct)
    {
        var messages = chunk.Select(to => new
        {
            to,
            title,
            body,
            sound = "default",
            // High priority = deliver immediately even in doze, and (with the
            // app's MAX-importance channel) show as a heads-up banner.
            priority = "high",
            channelId = "default",
            data = data ?? new Dictionary<string, string>(),
        });

        string text;
        int status;
        try
        {
            using var res = await client.PostAsJsonAsync(Endpoint, messages, ct);
            status = (int)res.StatusCode;
            text = await res.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Expo push send failed for {Count} tokens.", chunk.Count);
            return;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(text); }
        catch (JsonException)
        {
            _logger.LogWarning("Expo push returned {Status} with a non-JSON body for {Count} tokens: {Body}",
                status, chunk.Count, Truncate(text));
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;

            // Request-level errors (the whole batch was refused).
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
                    if (code == MixedProjectsCode && allowSplit
                        && error.TryGetProperty("details", out var details)
                        && details.ValueKind == JsonValueKind.Object)
                    {
                        // Expo tells us which token belongs to which project; resend one
                        // request per project, exactly as it asks.
                        foreach (var project in details.EnumerateObject())
                        {
                            if (project.Value.ValueKind != JsonValueKind.Array) continue;
                            var group = project.Value.EnumerateArray()
                                .Select(t => t.GetString())
                                .Where(t => !string.IsNullOrWhiteSpace(t))
                                .Select(t => t!)
                                .ToList();
                            if (group.Count > 0)
                                await SendChunkAsync(client, group, title, body, data, dead, allowSplit: false, ct);
                        }
                        return;
                    }

                    _logger.LogWarning("Expo push refused {Count} tokens ({Status}): {Code} {Message}",
                        chunk.Count, status, code,
                        error.TryGetProperty("message", out var m) ? m.GetString() : Truncate(text));
                }
                return;
            }

            if (status is < 200 or >= 300)
            {
                _logger.LogWarning("Expo push returned {Status} for {Count} tokens: {Body}",
                    status, chunk.Count, Truncate(text));
                return;
            }

            // Per-message tickets, in the same order as the messages we sent.
            if (!root.TryGetProperty("data", out var tickets) || tickets.ValueKind != JsonValueKind.Array)
                return;

            var index = 0;
            foreach (var ticket in tickets.EnumerateArray())
            {
                var token = index < chunk.Count ? chunk[index] : null;
                index++;
                if (token is null) break;

                var ticketStatus = ticket.TryGetProperty("status", out var s) ? s.GetString() : null;
                if (ticketStatus == "ok") continue;

                string? detailError = null;
                if (ticket.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object
                    && d.TryGetProperty("error", out var e))
                    detailError = e.GetString();

                if (detailError == DeviceNotRegistered)
                {
                    dead.Add(token);
                    continue;
                }

                _logger.LogWarning("Expo push ticket error for one token: {Error} {Message}",
                    detailError ?? ticketStatus,
                    ticket.TryGetProperty("message", out var tm) ? tm.GetString() : null);
            }
        }
    }

    private static string Truncate(string s) => s.Length <= 600 ? s : s[..600] + "…";
}
