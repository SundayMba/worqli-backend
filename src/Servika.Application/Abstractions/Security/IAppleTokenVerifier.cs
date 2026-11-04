namespace Servika.Application.Abstractions.Security;

/// <summary>The identity Apple attests to in a verified identity token.</summary>
/// <param name="Subject">Apple's stable user id for this developer team (the token's <c>sub</c>).</param>
/// <param name="Email">The Apple ID email, or a private relay address when the user chose "Hide My Email".</param>
public sealed record AppleUserInfo(string Subject, string Email);

/// <summary>
/// Verifies a Sign in with Apple identity token (the JWT the iPhone receives from Apple) and returns the
/// identity it attests to. Implementations must check the signature against Apple's published keys, the
/// issuer, expiry, AND that the audience is one of OUR app ids, or a token minted for any other app would
/// log its users into Servika.
/// </summary>
public interface IAppleTokenVerifier
{
    /// <summary>The verified identity, or null when the token is invalid, expired, for a different app, or
    /// Apple sign-in isn't configured.</summary>
    Task<AppleUserInfo?> VerifyAsync(string identityToken, CancellationToken ct);
}
