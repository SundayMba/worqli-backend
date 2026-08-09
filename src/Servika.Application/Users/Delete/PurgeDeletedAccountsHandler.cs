using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;

namespace Servika.Application.Users.Delete;

/// <summary>
/// Hard-erases accounts that were soft-deleted before a cutoff (grace period elapsed).
/// For each, it removes every database row and every uploaded file, exactly like the
/// admin "delete permanently" action. Run periodically by the background purge worker.
/// </summary>
public sealed class PurgeDeletedAccountsHandler
{
    private readonly IUserRepository _users;
    private readonly IAccountEraser _eraser;
    private readonly IFileStorage _storage;

    public PurgeDeletedAccountsHandler(
        IUserRepository users, IAccountEraser eraser, IFileStorage storage)
    {
        _users = users;
        _eraser = eraser;
        _storage = storage;
    }

    /// <summary>Purges all accounts soft-deleted before <paramref name="deletedBefore"/>;
    /// returns how many were erased.</summary>
    public async Task<int> RunAsync(DateTimeOffset deletedBefore, CancellationToken ct)
    {
        var ids = await _users.ListSoftDeletedBeforeAsync(deletedBefore, ct);

        foreach (var id in ids)
        {
            var fileKeys = await _eraser.EraseAsync(id, ct);
            foreach (var key in fileKeys)
                await _storage.DeleteAsync(key, ct);
        }

        return ids.Count;
    }
}
