using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Me;

/// <summary>
/// The "who am I" use case: given the user id extracted from a validated access
/// token, return the current profile plus roles/permissions. Called on app boot.
/// </summary>
public sealed class GetMeHandler
{
    private readonly IUserRepository _users;

    public GetMeHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<MeResponse> HandleAsync(Guid userId, CancellationToken ct)
    {
        // Token was valid but the account no longer exists (e.g. deleted) → 401.
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        return new MeResponse(
            User: UserMapping.ToDto(user),
            Roles: new[] { user.Role.ToString() },
            // Fine-grained permission map is a later RBAC slice; empty for now.
            Permissions: Array.Empty<string>());
    }
}
