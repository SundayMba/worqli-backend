namespace Servika.Application.Catalogue;

/// <summary>
/// An artisan's personal booking code (e.g. "EMEKA42") and link. Customers who book with it get
/// that artisan first (Business Model v1.4, section 15). Short enough to read out on a phone call
/// and to print on a card; the caller retries on the rare collision.
/// </summary>
public static class BookingCodes
{
    public const string LinkBase = "https://servika.com.ng/a/";
    public const int MaxLength = 16;

    public static string Generate(string fullName)
    {
        var first = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var letters = new string(first.Where(char.IsLetter).Take(6).ToArray()).ToUpperInvariant();
        if (letters.Length < 3) letters = "PRO";
        return $"{letters}{Random.Shared.Next(10, 99)}";
    }

    /// <summary>Upper-case letters and digits only, so "emeka-42", "Emeka 42" and a link all resolve.</summary>
    public static string Normalize(string? input)
    {
        var s = (input ?? string.Empty).Trim();
        var slash = s.LastIndexOf('/');
        if (slash >= 0) s = s[(slash + 1)..];
        return new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    public static string Link(string code) => LinkBase + code;
}
