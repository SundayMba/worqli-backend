namespace Servika.Contracts.Auth;

/// <summary>
/// What GET /api/v1/auth/me returns: the current user plus their roles and
/// permissions, read from the validated access token on app boot.
/// </summary>
/// <param name="User">The authenticated user's public profile.</param>
/// <param name="Roles">The user's roles (currently exactly one, e.g. "Customer").</param>
/// <param name="Permissions">Fine-grained permissions. Empty until the RBAC permission map lands.</param>
public sealed record MeResponse(
    UserDto User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
