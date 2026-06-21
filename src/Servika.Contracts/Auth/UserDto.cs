namespace Servika.Contracts.Auth;

/// <summary>
/// The safe, public view of a user that we send back to clients. Crucially it
/// has NO PasswordHash — the internal Domain.User keeps that; this DTO never
/// carries it outside the building.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role);
