using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Domain.Users;

namespace Servika.Application.Users.Delete;

/// <summary>
/// Deletes the signed-in user's account (a Play Store / GDPR-style requirement). This
/// is a SOFT delete: the account is hidden everywhere and can't sign in again, but the
/// data and files survive for a grace period so an accidental or regretted deletion is
/// recoverable, after which the background purge hard-erases everything. The artisan
/// profile is soft-deleted alongside so it leaves the marketplace immediately.
/// </summary>
public sealed class DeleteAccountHandler
{
    private readonly IUserRepository _users;
    private readonly ICatalogueRepository _catalogue;
    private readonly IClock _clock;

    public DeleteAccountHandler(IUserRepository users, ICatalogueRepository catalogue, IClock clock)
    {
        _users = users;
        _catalogue = catalogue;
        _clock = clock;
    }

    public async Task HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId, ct)
            ?? throw new NotFoundException("Account not found.");

        // The platform can never delete itself out of its own admin seat.
        if (user.Role is Role.Admin or Role.SuperAdmin)
            throw new ConflictException("Admin accounts cannot be deleted from the app.");

        var now = _clock.UtcNow;
        user.SoftDelete(now);
        var profile = await _catalogue.GetArtisanByUserIdForUpdateAsync(userId, ct);
        profile?.SoftDelete(now);

        await _users.SaveChangesAsync(ct);
        await _catalogue.SaveChangesAsync(ct);
    }
}
