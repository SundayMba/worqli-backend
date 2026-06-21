using System.Security.Cryptography;
using System.Text;
using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Generates one-time codes and reset tokens, and hashes them deterministically
/// with SHA-256 (hex). Deterministic so a stored code can be found by hashing
/// the value a user presents — fine for high-entropy reset tokens, and adequate
/// for short OTPs given their short expiry, attempt limit, and per-user scoping.
/// </summary>
public sealed class OtpService : IOtpService
{
    private const int ResetTokenBytes = 32; // 256 bits

    public string GenerateNumericCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public string GenerateResetToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(ResetTokenBytes))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes); // uppercase hex, 64 chars
    }
}
