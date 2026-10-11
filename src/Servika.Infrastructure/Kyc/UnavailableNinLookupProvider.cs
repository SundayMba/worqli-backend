using Servika.Application.Abstractions.Kyc;

namespace Servika.Infrastructure.Kyc;

/// <summary>
/// Production stand-in when no NIN provider key is set: reports itself as not
/// configured, so every lookup comes back Unavailable. Never fabricates a match
/// (that is the dev stub's job, and it must never run live).
/// </summary>
public sealed class UnavailableNinLookupProvider : INinLookupProvider
{
    public bool IsConfigured => false;

    public Task<NinLookupResult?> LookupAsync(string nin, string? hintFullName, CancellationToken ct) =>
        throw new InvalidOperationException("No NIN lookup provider is configured.");
}
