using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Contracts.Auth;

namespace Servika.Application.Users.Profile;

/// <summary>Updates the signed-in user's name + phone and returns the fresh profile.</summary>
public sealed class UpdateProfileHandler
{
    private readonly IUserRepository _users;

    public UpdateProfileHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<UserDto> HandleAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new InvalidCredentialsException();

        user.UpdateProfile(request.FullName, request.PhoneNumber);
        await _users.SaveChangesAsync(ct);

        return UserMapping.ToDto(user);
    }
}
