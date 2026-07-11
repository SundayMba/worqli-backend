namespace Servika.Contracts.Auth;

/// <summary>
/// Sign in / sign up with Google (POST /api/v1/auth/google). The app runs the
/// native Google sign-in flow and sends the resulting ID token; the server
/// verifies it with Google and issues a normal Servika session.
/// </summary>
/// <param name="IdToken">The Google ID token from the device sign-in flow.</param>
public sealed record GoogleLoginRequest(string IdToken);
