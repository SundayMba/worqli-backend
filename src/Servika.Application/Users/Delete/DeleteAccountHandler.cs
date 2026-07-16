using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;
using Servika.Domain.Users;

namespace Servika.Application.Users.Delete;

/// <summary>
/// Permanently deletes the signed-in user's account (a Play Store / GDPR-style
/// requirement). Related rows cascade in the database (bookings, notifications,
/// tokens, chat, favourites…); an artisan's marketplace profile is unlinked
/// (SetNull) rather than deleted, and the append-only wallet ledger is kept —
/// financial records survive account deletion by design.
/// </summary>
public sealed class DeleteAccountHandler
{
    private readonly IUserRepository _users;

    public DeleteAccountHandler(IUserRepository users)
    {
        _users = users;
    }

    public async Task HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        // The platform can never delete itself out of its own admin seat.
        if (user.Role is Role.Admin or Role.SuperAdmin)
            throw new ConflictException("Admin accounts cannot be deleted from the app.");

        _users.RemoveUser(user);
        await _users.SaveChangesAsync(ct);
    }
}
