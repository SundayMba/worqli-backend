namespace Servika.Contracts.Admin;

/// <summary>A user in the admin directory.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    bool EmailVerified,
    bool IsSuspended,
    DateTimeOffset CreatedAt,
    /// <summary>When the account was soft-deleted (recoverable / pending purge), else null.</summary>
    DateTimeOffset? DeletedAtUtc);
