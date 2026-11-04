using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Servika.Infrastructure.Security;

namespace Servika.UnitTests.Auth;

/// <summary>
/// The Apple verifier against a fake "Apple keys" endpoint serving a test key: only a token signed by that
/// key, issued by Apple, for one of our app ids and not expired is accepted.
/// </summary>
public class AppleTokenVerifierTests
{
    private const string Kid = "test-kid";
    private const string Issuer = "https://appleid.apple.com";
    private static readonly RSA AppleKey = RSA.Create(2048);

    private sealed class FakeHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private sealed class FakeFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static AppleTokenVerifier Verifier(string bundleIds = "com.servika.app,com.servika.pro")
    {
        var p = AppleKey.ExportParameters(false);
        var jwks = JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new { kty = "RSA", kid = Kid, use = "sig", alg = "RS256",
                      n = Base64UrlEncoder.Encode(p.Modulus), e = Base64UrlEncoder.Encode(p.Exponent) },
            },
        });
        return new AppleTokenVerifier(new FakeFactory(new FakeHandler(jwks)),
            new AppleSignInOptions { BundleIds = bundleIds }, NullLogger<AppleTokenVerifier>.Instance);
    }

    private static string Token(string aud = "com.servika.app", string iss = Issuer, int minutesLeft = 10,
        RSA? key = null, string? email = "ada@privaterelay.appleid.com")
    {
        var claims = new List<Claim> { new("sub", "001234.abcd"), new("email_verified", "true") };
        if (email is not null) claims.Add(new("email", email));
        var now = DateTime.UtcNow;
        var creds = new SigningCredentials(new RsaSecurityKey(key ?? AppleKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        var jwt = new JwtSecurityToken(iss, aud, claims, now.AddMinutes(-20), now.AddMinutes(minutesLeft), creds);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    [Fact]
    public async Task Accepts_a_genuine_token_for_our_app()
    {
        var who = await Verifier().VerifyAsync(Token(), CancellationToken.None);
        Assert.NotNull(who);
        Assert.Equal("001234.abcd", who!.Subject);
        Assert.Equal("ada@privaterelay.appleid.com", who.Email);
    }

    [Fact]
    public async Task Accepts_the_Pro_app_too() =>
        Assert.NotNull(await Verifier().VerifyAsync(Token(aud: "com.servika.pro"), CancellationToken.None));

    [Fact]
    public async Task Refuses_a_token_for_another_app() =>
        Assert.Null(await Verifier().VerifyAsync(Token(aud: "com.someone.else"), CancellationToken.None));

    [Fact]
    public async Task Refuses_a_token_not_issued_by_Apple() =>
        Assert.Null(await Verifier().VerifyAsync(Token(iss: "https://evil.example"), CancellationToken.None));

    [Fact]
    public async Task Refuses_an_expired_token() =>
        Assert.Null(await Verifier().VerifyAsync(Token(minutesLeft: -10), CancellationToken.None));

    [Fact]
    public async Task Refuses_a_forged_signature() =>
        Assert.Null(await Verifier().VerifyAsync(Token(key: RSA.Create(2048)), CancellationToken.None));

    [Fact]
    public async Task Refuses_a_token_without_an_email() =>
        Assert.Null(await Verifier().VerifyAsync(Token(email: null), CancellationToken.None));

    [Fact]
    public async Task Is_off_until_app_ids_are_configured() =>
        Assert.Null(await Verifier(bundleIds: "").VerifyAsync(Token(), CancellationToken.None));

    [Fact]
    public async Task Refuses_garbage() =>
        Assert.Null(await Verifier().VerifyAsync("not-a-jwt", CancellationToken.None));
}
