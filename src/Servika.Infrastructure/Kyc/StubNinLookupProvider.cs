using Servika.Application.Abstractions.Kyc;

namespace Servika.Infrastructure.Kyc;

/// <summary>
/// Offline stand-in for the register so the identity flow is testable without a paid
/// key. Any eleven digits resolve to the account holder's own name (a match); a NIN
/// ending in 00 is "not found"; one ending in 99 resolves to a different person.
/// Reports itself as configured so the flow behaves like production locally.
/// </summary>
public sealed class StubNinLookupProvider : INinLookupProvider
{
    public bool IsConfigured => true;

    public Task<NinLookupResult?> LookupAsync(string nin, string? hintFullName, CancellationToken ct)
    {
        if (nin.EndsWith("00")) return Task.FromResult<NinLookupResult?>(null);
        if (nin.EndsWith("99")) return Task.FromResult<NinLookupResult?>(new NinLookupResult("Adaeze", "Nwosu", "Chi", "1990-01-01", null));
        var parts = (hintFullName ?? "Test Artisan").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : "Test";
        var last = parts.Length > 1 ? parts[^1] : "Artisan";
        var middle = parts.Length > 2 ? parts[1] : null;
        return Task.FromResult<NinLookupResult?>(new NinLookupResult(first, last, middle, null, null));
    }
}
