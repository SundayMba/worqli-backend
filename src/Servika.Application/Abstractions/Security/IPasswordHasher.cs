namespace Servika.Application.Abstractions.Security;

/// <summary>
/// Turns a raw password into a safe-to-store hash, and checks a password against
/// a stored hash. The Application layer depends only on this contract — it never
/// knows which algorithm (BCrypt, Argon2, …) is used. Infrastructure supplies
/// the real implementation, which keeps the choice swappable and testable.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a raw password for storage. Never store the raw value.</summary>
    string Hash(string password);

    /// <summary>Returns true if <paramref name="password"/> matches <paramref name="hash"/>.</summary>
    bool Verify(string password, string hash);
}
