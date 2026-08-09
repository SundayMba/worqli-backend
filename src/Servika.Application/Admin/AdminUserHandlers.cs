using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Admin;
using Servika.Domain.Users;

namespace Servika.Application.Admin;

/// <summary>Maps a user to the admin directory DTO.</summary>
internal static class AdminUserMapping
{
    public static AdminUserDto ToAdminDto(this User u) =>
        new(u.Id, u.FullName, u.Email, u.PhoneNumber, u.Role.ToString(),
            u.EmailVerifiedAtUtc is not null, u.IsSuspended, u.CreatedAt);
}

/// <summary>The admin user directory, newest first, optional role filter.</summary>
public sealed class ListUsersHandler
{
    private readonly IUserRepository _users;

    public ListUsersHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task<IReadOnlyList<AdminUserDto>> HandleAsync(string? role, CancellationToken ct)
    {
        Role? filter = null;
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<Role>(role, ignoreCase: true, out var parsed))
                throw new ArgumentException($"'{role}' is not a valid role.");
            filter = parsed;
        }

        var users = await _users.ListAsync(filter, ct);
        return users.Select(u => u.ToAdminDto()).ToList();
    }
}

/// <summary>Admin suspends or reactivates an account (blocks/allows sign-in).</summary>
public sealed class SetUserSuspendedHandler
{
    private readonly IUserRepository _users;
    private readonly IClock _clock;

    public SetUserSuspendedHandler(IUserRepository users, IClock clock)
    {
        _users = users;
        _clock = clock;
    }

    public async Task<AdminUserDto> HandleAsync(Guid userId, bool suspend, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        if (suspend && user.Role is Role.Admin or Role.SuperAdmin)
            throw new ConflictException("Admin accounts cannot be suspended.");

        if (suspend) user.Suspend(_clock.UtcNow);
        else user.Reactivate();

        await _users.SaveChangesAsync(ct);
        return user.ToAdminDto();
    }
}

/// <summary>
/// Permanently deletes a customer or artisan account and everything tied to it —
/// the artisan profile, KYC, bookings, ledger, reviews, bids, chats, favourites,
/// referrals — plus every uploaded file (KYC images, profile/cover/certificate/
/// gallery photos, booking photos/videos). Admin accounts are protected. The
/// database rows go in one transaction (<see cref="IAccountEraser"/>); the storage
/// files are deleted best-effort afterwards, so a blob-store hiccup can't undo the
/// database erase.
/// </summary>
public sealed class AdminDeleteUserHandler
{
    private readonly IUserRepository _users;
    private readonly IAccountEraser _eraser;
    private readonly Abstractions.Storage.IFileStorage _storage;

    public AdminDeleteUserHandler(
        IUserRepository users,
        IAccountEraser eraser,
        Abstractions.Storage.IFileStorage storage)
    {
        _users = users;
        _eraser = eraser;
        _storage = storage;
    }

    public async Task HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User '{userId}' was not found.");

        if (user.Role is Role.Admin or Role.SuperAdmin)
            throw new ConflictException("Admin accounts cannot be deleted.");

        var fileKeys = await _eraser.EraseAsync(userId, ct);

        foreach (var key in fileKeys)
            await _storage.DeleteAsync(key, ct);
    }
}
