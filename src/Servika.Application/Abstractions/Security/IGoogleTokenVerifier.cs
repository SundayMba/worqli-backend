namespace Servika.Application.Abstractions.Security;

/// <summary>The identity Google attests to in a verified ID token.</summary>
/// <param name="Email">The Google account email (Google has verified ownership).</param>
/// <param name="FullName">Display name from the Google profile (may be empty).</param>
public sealed record GoogleUserInfo(string Email, string FullName);

/// <summary>
/// Verifies a Google Sign-In ID token (the JWT the mobile app receives from the
/// Google sign-in flow) and returns the identity it attests to. Implementations
/// must check the token's signature/expiry AND that its audience is one of OUR
/// OAuth client ids — otherwise a token minted for any other app would log its
/// users into Servika.
/// </summary>
public interface IGoogleTokenVerifier
{
    /// <summary>The verified identity, or null when the token is invalid,
    /// expired, for a different app, or Google sign-in isn't configured.</summary>
    Task<GoogleUserInfo?> VerifyAsync(string idToken, CancellationToken ct);
}
