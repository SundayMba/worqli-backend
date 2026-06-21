using Servika.Application.Abstractions.Notifications;
using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Security;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Application.Users.Otp;
using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users.Register;

/// <summary>
/// The "register a new customer" use case. Registration does NOT log the user in:
/// it creates an unverified account and emails a 6-digit verification code. The
/// app then submits that code to <c>verify-otp</c>, which marks the email verified
/// and issues the session. So no tokens are minted here.
///
/// Depends only on abstractions (interfaces) — never on EF Core, BCrypt, or the
/// web — so the whole flow is unit-testable with fakes.
/// </summary>
public sealed class RegisterUserHandler
{
    private readonly IUserRepository _users;
    private readonly IVerificationCodeRepository _codes;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otp;
    private readonly IOtpSender _sender;
    private readonly IClock _clock;

    public RegisterUserHandler(
        IUserRepository users,
        IVerificationCodeRepository codes,
        IPasswordHasher passwordHasher,
        IOtpService otp,
        IOtpSender sender,
        IClock clock)
    {
        _users = users;
        _codes = codes;
        _passwordHasher = passwordHasher;
        _otp = otp;
        _sender = sender;
        _clock = clock;
    }

    public async Task<RegisterResponse> HandleAsync(RegisterRequest request, CancellationToken ct)
    {
        // Minimal guard. Richer validation (formats, etc.) comes in a later slice.
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(request));

        // Normalise the same way the Domain does, so the duplicate check matches.
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(normalizedEmail, ct))
            throw new EmailAlreadyInUseException(normalizedEmail);

        var now = _clock.UtcNow;

        // 1. Hash the password (Infrastructure/BCrypt), never store the raw value.
        var passwordHash = _passwordHasher.Hash(request.Password);

        // 2. Create the user via the Domain factory (which re-validates + normalises).
        //    The account starts unverified — no session is granted yet.
        var user = User.Register(
            fullName: request.FullName,
            email: request.Email,
            phoneNumber: request.PhoneNumber,
            passwordHash: passwordHash,
            role: ResolveRole(request.Role),
            createdAt: now);
        _users.AddUser(user);

        // 3. Issue an email verification code and stage it alongside the user.
        var ttl = OtpPolicy.TtlSecondsFor(OtpPurpose.AccountVerification);
        var code = _otp.GenerateNumericCode();
        _codes.Add(VerificationCode.Issue(
            user.Id, OtpPurpose.AccountVerification, _otp.Hash(code), now.AddSeconds(ttl), now));

        // 4. Send the code BEFORE committing: if delivery fails, nothing is
        //    persisted, so the user can simply retry registration cleanly.
        await _sender.SendAsync(user.Email, code, OtpPurpose.AccountVerification, ct);

        // 5. Commit the user + verification code together in one save.
        await _users.SaveChangesAsync(ct);

        return new RegisterResponse(Email: user.Email, VerificationRequired: true);
    }

    // Self-registration may only create Customer or Artisan accounts. Admin and
    // SuperAdmin are provisioned internally (seed/admin tooling), never via signup.
    private static Role ResolveRole(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return Role.Customer;

        return requested.Trim().ToLowerInvariant() switch
        {
            "customer" => Role.Customer,
            "artisan" => Role.Artisan,
            _ => throw new ArgumentException(
                "Role must be 'Customer' or 'Artisan'.", nameof(requested)),
        };
    }
}
