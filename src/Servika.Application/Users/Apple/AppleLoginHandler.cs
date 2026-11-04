using Servika.Application.Abstractions.Security;
using Servika.Application.Common;
using Servika.Application.Users.Social;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Apple;

/// <summary>
/// Sign in (or sign up) with Apple, for iPhone users. The app hands us Apple's identity token; we verify it
/// against Apple's keys, then <see cref="SocialSignInService"/> logs the matching account in or creates a
/// fresh Customer account. Apple only shares the name on the first sign-in, so the app forwards it. A user who
/// chose "Hide My Email" gets an account under their private relay address.
/// </summary>
public sealed class AppleLoginHandler
{
    private readonly IAppleTokenVerifier _apple;
    private readonly SocialSignInService _signIn;

    public AppleLoginHandler(IAppleTokenVerifier apple, SocialSignInService signIn)
    {
        _apple = apple;
        _signIn = signIn;
    }

    public async Task<AuthResponse> HandleAsync(AppleLoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
            throw new InvalidCredentialsException();

        var identity = await _apple.VerifyAsync(request.IdentityToken, ct)
            ?? throw new InvalidCredentialsException();

        return await _signIn.SignInAsync(identity.Email, request.FullName, ct);
    }
}
