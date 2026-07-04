using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Notifications;

namespace Servika.Infrastructure.Notifications;

/// <summary>
/// Sends push notifications via Expo's push service (https://exp.host). No API key
/// is needed to send to Expo push tokens. Best-effort: any failure is logged and
/// swallowed — a push must never break the flow that triggered it. Expo accepts up
/// to 100 messages per request, so tokens are chunked.
/// </summary>
public sealed class ExpoPushSender : IPushSender
{
    private const string Endpoint = "https://exp.host/--/api/v2/push/send";
    private const int ChunkSize = 100;

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(IHttpClientFactory httpFactory, ILogger<ExpoPushSender> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task SendAsync(
        IReadOnlyCollection<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data,
        CancellationToken ct)
    {
        if (tokens.Count == 0) return;

        var client = _httpFactory.CreateClient("expo-push");
        var all = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

        for (var i = 0; i < all.Count; i += ChunkSize)
        {
            var chunk = all.Skip(i).Take(ChunkSize).Select(to => new
            {
                to,
                title,
                body,
                sound = "default",
                data = data ?? new Dictionary<string, string>(),
            });

            try
            {
                var res = await client.PostAsJsonAsync(Endpoint, chunk, ct);
                if (!res.IsSuccessStatusCode)
                    _logger.LogWarning("Expo push returned {Status} for {Count} tokens.",
                        (int)res.StatusCode, all.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Expo push send failed for {Count} tokens.", all.Count);
            }
        }
    }
}
