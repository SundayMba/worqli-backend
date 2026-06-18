using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// BCrypt-based implementation of <see cref="IPasswordHasher"/>. BCrypt is a
/// deliberately slow, salted hashing algorithm designed for passwords: each hash
/// embeds its own random salt and a work factor, so identical passwords produce
/// different hashes and brute-forcing stays expensive.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor (cost). Higher = slower = harder to brute-force. 12 is a sane
    // modern default; raise it as hardware gets faster.
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
