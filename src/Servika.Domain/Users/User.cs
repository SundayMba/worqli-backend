namespace Servika.Domain.Users;

/// <summary>
/// A person who can sign in to Servika. Every customer, artisan and admin is a
/// User. This entity lives in the Domain layer, so it deliberately knows nothing
/// about the database, the web, or how passwords are hashed. It only holds the
/// identity data and the rules that must always be true about a user.
/// </summary>
public sealed class User
{
    // `private set` means these can be read from anywhere, but only changed from
    // inside this class. So the only way to get a User is through Register()
    // below — there's no way to build one in an invalid state from outside.
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;

    // We store ONLY the hash, never the raw password. The hashing itself happens
    // in an outer layer (Infrastructure) and the finished hash is handed in here.
    public string PasswordHash { get; private set; } = string.Empty;

    public Role Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the user's email/phone was OTP-verified. Null until verified.</summary>
    public DateTimeOffset? EmailVerifiedAtUtc { get; private set; }

    /// <summary>When the user's phone number was OTP-verified (SMS/WhatsApp). Null
    /// until verified — phone is verified at first booking/chat, not at signup.</summary>
    public DateTimeOffset? PhoneVerifiedAtUtc { get; private set; }

    /// <summary>True once the phone number has been confirmed by OTP.</summary>
    public bool IsPhoneVerified => PhoneVerifiedAtUtc is not null;

    /// <summary>The user's own share code others enter to be referred by them.
    /// Assigned lazily (on first view / at registration).</summary>
    public string? ReferralCode { get; private set; }

    /// <summary>When an admin suspended the account. Null = active. A suspended
    /// account can't sign in.</summary>
    public DateTimeOffset? SuspendedAtUtc { get; private set; }

    /// <summary>Whether the account is currently suspended (blocked from signing in).</summary>
    public bool IsSuspended => SuspendedAtUtc is not null;

    /// <summary>When the account was soft-deleted. Null = live. A soft-deleted account
    /// is hidden everywhere (a global query filter excludes it) and can't sign in; the
    /// row and its files survive for a grace period, then a background purge hard-erases
    /// it. This makes an accidental or regretted deletion recoverable.</summary>
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>Whether the account has been soft-deleted.</summary>
    public bool IsDeleted => DeletedAtUtc is not null;

    // EF Core needs a parameterless constructor to rebuild a User from a database
    // row. It's private so normal application code can't use it to skip our rules.
    private User() { }


    private User(
        Guid id,
        string fullName,
        string email,
        string phoneNumber,
        string passwordHash,
        Role role,
        DateTimeOffset createdAt)
    {
        Id = id;
        FullName = fullName;
        Email = email;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a brand-new user account. The email is normalised (trimmed and
    /// lower-cased) so "Ada@X.com" and "ada@x.com" count as the same login.
    /// `passwordHash` is already-hashed — this method never sees a raw password.
    /// </summary>
    public static User Register(
        string fullName,
        string email,
        string phoneNumber,
        string passwordHash,
        Role role,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));

        return new User(
            id: Guid.NewGuid(),
            fullName: fullName.Trim(),
            email: email.Trim().ToLowerInvariant(),
            phoneNumber: phoneNumber.Trim(),
            passwordHash: passwordHash,
            role: role,
            createdAt: createdAt);
    }

    /// <summary>Replaces the stored password hash (e.g. after a password reset).</summary>
    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.", nameof(newPasswordHash));
        PasswordHash = newPasswordHash;
    }

    /// <summary>Marks the account as verified after a successful OTP check.</summary>
    public void MarkEmailVerified(DateTimeOffset whenUtc) => EmailVerifiedAtUtc = whenUtc;

    /// <summary>Records that the phone number has been OTP-verified. Idempotent.</summary>
    public void MarkPhoneVerified(DateTimeOffset whenUtc) => PhoneVerifiedAtUtc ??= whenUtc;

    /// <summary>Updates the editable profile fields (name + phone). Email is
    /// immutable here — changing it would need re-verification (a later slice).</summary>
    public void UpdateProfile(string fullName, string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name is required.", nameof(fullName));
        FullName = fullName.Trim();
        PhoneNumber = phoneNumber?.Trim() ?? string.Empty;
    }

    /// <summary>Assigns the user's referral share code (once).</summary>
    public void SetReferralCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Referral code is required.", nameof(code));
        ReferralCode ??= code.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Converts a customer account into an artisan ("Become a Pro"). Idempotent for
    /// an existing artisan; admins can't self-convert.
    /// </summary>
    public void PromoteToArtisan()
    {
        if (Role is Role.Admin or Role.SuperAdmin)
            throw new InvalidOperationException("Admin accounts cannot become artisans.");
        Role = Role.Artisan;
    }

    /// <summary>Admin action: suspend the account (blocks sign-in). Admin/SuperAdmin
    /// accounts can't be suspended, so the platform can't be locked out of itself.</summary>
    public void Suspend(DateTimeOffset now)
    {
        if (Role is Role.Admin or Role.SuperAdmin)
            throw new InvalidOperationException("Admin accounts cannot be suspended.");
        SuspendedAtUtc ??= now;
    }

    /// <summary>Admin action: lift a suspension.</summary>
    public void Reactivate() => SuspendedAtUtc = null;

    /// <summary>Soft-delete the account (recoverable until purged).</summary>
    public void SoftDelete(DateTimeOffset now) => DeletedAtUtc ??= now;

    /// <summary>Undo a soft-delete, bringing the account back to life.</summary>
    public void Restore() => DeletedAtUtc = null;
}
