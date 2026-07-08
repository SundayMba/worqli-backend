using System.Text.RegularExpressions;

namespace Servika.Application.Chat;

/// <summary>
/// Anti-disintermediation filter for chat. A marketplace loses the booking (and its
/// escrow protection, reviews, dispute cover and — once live — commission) if the two
/// parties swap contact details and take the job off-app. We can't stop it entirely,
/// but redacting the concrete hand-off vectors (phone numbers + emails) in message
/// bodies raises the friction and signals the policy. Applied <b>server-side</b> on
/// send, so it can't be bypassed by a modified client and history stays clean.
/// </summary>
internal static class ChatContentPolicy
{
    private const string Hidden = "[hidden]";

    private static readonly Regex Email = new(
        @"[\w.+-]+@[\w-]+\.[\w.-]+", RegexOptions.Compiled);

    // A digit, then a run of digits/separators, ending in a digit — a phone-number
    // shape. We only redact when the run actually holds ≥7 digits, so short things
    // like "9-11am" or a "₦5,000" price survive.
    private static readonly Regex PhoneLike = new(
        @"\+?\d[\d\s\-().]{5,}\d", RegexOptions.Compiled);

    /// <summary>Redacts emails + phone-number-shaped runs to <c>[hidden]</c>.</summary>
    public static string Redact(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var redacted = Email.Replace(body, Hidden);
        redacted = PhoneLike.Replace(
            redacted,
            m => m.Value.Count(char.IsDigit) >= 7 ? Hidden : m.Value);
        return redacted;
    }
}
