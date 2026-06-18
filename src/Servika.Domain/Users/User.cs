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
}
