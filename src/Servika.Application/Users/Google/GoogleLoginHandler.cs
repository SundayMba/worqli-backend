using Servika.Application.Abstractions.Security;
using Servika.Application.Common;
using Servika.Application.Users.Social;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Google;

/// <summary>
/// Sign in (or sign up) with Google. The app hands us the Google ID token; we verify it with Google, then
/// <see cref="SocialSignInService"/> logs the matching account in or creates a fresh Customer account. Google
/// has already verified ownership of the email, so a session is issued immediately.
/// </summary>
public sealed class GoogleLoginHandler
{
    private readonly IGoogleTokenVerifier _google;
    private readonly SocialSignInService _signIn;

    public GoogleLoginHandler(IGoogleTokenVerifier google, SocialSignInService signIn)
    {
        _google = google;
        _signIn = signIn;
    }

    public async Task<AuthResponse> HandleAsync(GoogleLoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            throw new InvalidCredentialsException();

        var identity = await _google.VerifyAsync(request.IdToken, ct)
            ?? throw new InvalidCredentialsException();

        return await _signIn.SignInAsync(identity.Email, identity.FullName, ct);
    }
}
