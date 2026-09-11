using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Kyc;

namespace Servika.Infrastructure.Kyc;

/// <summary>
/// NIN lookup through Dojah (GET /api/v1/kyc/nin?nin=). Needs the dashboard app id and
/// a secret key; each successful lookup is billed. A 404 from Dojah means the NIN is
/// not on the register; anything else non-2xx surfaces as a provider failure.
/// </summary>
public sealed class DojahNinLookupProvider : INinLookupProvider
{
    private readonly IHttpClientFactory _http;
    private readonly NinOptions _options;
    private readonly ILogger<DojahNinLookupProvider> _log;

    public DojahNinLookupProvider(IHttpClientFactory http, NinOptions options, ILogger<DojahNinLookupProvider> log)
    {
        _http = http;
        _options = options;
        _log = log;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<NinLookupResult?> LookupAsync(string nin, string? hintFullName, CancellationToken ct)
    {
        var client = _http.CreateClient("dojah");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl.TrimEnd('/')}/api/v1/kyc/nin?nin={Uri.EscapeDataString(nin)}");
        req.Headers.TryAddWithoutValidation("AppId", _options.AppId);
        req.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);
        using var res = await client.SendAsync(req, ct);
        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("Dojah NIN lookup failed with {Status}", (int)res.StatusCode);
            throw new HttpRequestException($"Dojah returned {(int)res.StatusCode}.");
        }
        var body = await res.Content.ReadFromJsonAsync<DojahEnvelope>(cancellationToken: ct);
        var e = body?.Entity;
        if (e is null || string.IsNullOrWhiteSpace(e.FirstName) && string.IsNullOrWhiteSpace(e.LastName)) return null;
        return new NinLookupResult(e.FirstName ?? "", e.LastName ?? "", e.MiddleName, e.DateOfBirth, e.PhoneNumber);
    }

    private sealed class DojahEnvelope
    {
        [JsonPropertyName("entity")] public DojahEntity? Entity { get; set; }
    }

    private sealed class DojahEntity
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        [JsonPropertyName("middle_name")] public string? MiddleName { get; set; }
        [JsonPropertyName("date_of_birth")] public string? DateOfBirth { get; set; }
        [JsonPropertyName("phone_number")] public string? PhoneNumber { get; set; }
    }
}
