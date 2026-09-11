namespace Servika.Application.Abstractions.Kyc;

/// <summary>What a NIN register lookup returns for a valid number.</summary>
public sealed record NinLookupResult(
    string FirstName,
    string LastName,
    string? MiddleName,
    string? DateOfBirth,
    string? Phone);

/// <summary>
/// Port for the national identity register. Real providers (Dojah, Prembly, Smile ID,
/// VerifyMe) all need an API key and charge per lookup; the dev stub answers offline.
/// </summary>
public interface INinLookupProvider
{
    /// <summary>True when a real provider is configured; false means every check is Unavailable.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns the register entry, or null when the NIN is not found. Throws on provider failure.</summary>
    /// <param name="hintFullName">The account holder's name, used only by the dev stub to fabricate a match.</param>
    Task<NinLookupResult?> LookupAsync(string nin, string? hintFullName, CancellationToken ct);
}
