using System.Text.Json;
using Microsoft.Extensions.Logging;
using Servika.Application.Abstractions.Payments;

namespace Servika.Infrastructure.Payments;

/// <summary>
/// Fetches the Nigerian bank list (name + NIP code) from Paystack's <c>/bank</c>
/// endpoint for the withdrawal picker. The result rarely changes, so it's cached
/// in memory for the process; on any API error it falls back to the static
/// <see cref="StubBankDirectory"/> list so the picker never breaks.
/// </summary>
public sealed class PaystackBankDirectory : IBankDirectory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaystackOptions _options;
    private readonly ILogger<PaystackBankDirectory> _logger;
    private readonly StubBankDirectory _fallback = new();

    private IReadOnlyList<BankInfo>? _cache;

    public PaystackBankDirectory(
        IHttpClientFactory httpClientFactory,
        PaystackOptions options,
        ILogger<PaystackBankDirectory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BankInfo>> ListBanksAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        try
        {
            var client = _httpClientFactory.CreateClient("paystack");
            client.BaseAddress = new Uri(_options.BaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.SecretKey);

            using var response = await client.GetAsync(
                "/bank?currency=NGN&perPage=100", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack /bank failed ({Status}); using fallback list.", response.StatusCode);
                return await _fallback.ListBanksAsync(ct);
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var banks = new List<BankInfo>(data.GetArrayLength());
            foreach (var b in data.EnumerateArray())
            {
                var name = b.TryGetProperty("name", out var n) ? n.GetString() : null;
                var code = b.TryGetProperty("code", out var c) ? c.GetString() : null;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(code))
                    banks.Add(new BankInfo(name, code));
            }

            if (banks.Count == 0) return await _fallback.ListBanksAsync(ct);
            _cache = banks
                .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Paystack /bank threw; using fallback list.");
            return await _fallback.ListBanksAsync(ct);
        }
    }
}
