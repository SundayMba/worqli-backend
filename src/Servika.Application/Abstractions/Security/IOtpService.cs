namespace Servika.Application.Abstractions.Security;

/// <summary>
/// Generates one-time secrets and hashes them for storage/lookup. The hash is
/// DETERMINISTIC (same input → same output), unlike a password hash, so we can
/// find a stored code by hashing the value a user presents. Infrastructure owns
/// the randomness source and the hash algorithm (SHA-256).
/// </summary>
public interface IOtpService
{
    /// <summary>A short numeric code for human entry (e.g. "482913").</summary>
    string GenerateNumericCode();

    /// <summary>A long, unguessable token for reset links (self-identifying).</summary>
    string GenerateResetToken();

    /// <summary>Deterministic hash used both to store a code and to look it up.</summary>
    string Hash(string code);
}
