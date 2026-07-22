using Servika.Contracts.Auth;
using Servika.Domain.Users;

namespace Servika.Application.Users;

/// <summary>Maps the internal Domain.User to the safe public UserDto (no hash).</summary>
internal static class UserMapping
{
    public static UserDto ToDto(User user) => new(
        Id: user.Id,
        FullName: user.FullName,
        Email: user.Email,
        PhoneNumber: user.PhoneNumber,
        Role: user.Role.ToString(),
        PhoneVerified: user.IsPhoneVerified);
}
