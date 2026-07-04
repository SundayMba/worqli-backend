namespace Servika.Application.Referrals;

/// <summary>
/// Builds short, human-friendly referral share codes (e.g. "MODUPE7423"): a slug
/// of the user's name + random digits. The caller retries on the rare collision.
/// </summary>
internal static class ReferralCodeGenerator
{
    public static string Generate(string fullName)
    {
        var letters = new string((fullName ?? string.Empty)
            .Where(char.IsLetter)
            .Take(6)
            .ToArray())
            .ToUpperInvariant();
        if (letters.Length < 3) letters = "SERVI";
        var digits = Random.Shared.Next(1000, 9999);
        return $"{letters}{digits}";
    }
}
