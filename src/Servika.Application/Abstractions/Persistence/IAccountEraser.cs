namespace Servika.Application.Abstractions.Persistence;

/// <summary>
/// Permanently and completely erases a user account (and, if they have one, their
/// artisan marketplace profile) together with every row that belongs to it — across
/// tables that cascade from the user AND the many that don't (data keyed by the
/// artisan's profile id or by owner id, which have no cascading foreign key). This is
/// the hard-delete behind the admin "delete account" action.
///
/// <para>Returns the storage keys of every file the account owned (KYC images,
/// profile/cover/certificate/gallery photos, and each booking's photos/video/
/// completion photos) so the caller can delete them from blob storage after the
/// database rows are gone.</para>
/// </summary>
public interface IAccountEraser
{
    Task<IReadOnlyList<string>> EraseAsync(Guid userId, CancellationToken ct);
}
