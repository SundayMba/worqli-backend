namespace Servika.Application.Abstractions.Storage;

/// <summary>
/// Port over blob storage for uploaded files (KYC documents, later job photos).
/// Infrastructure decides where bytes live — local disk in dev, S3 in production —
/// so the Application layer only ever deals in opaque string keys.
/// </summary>
public interface IFileStorage
{
    /// <summary>Stores bytes and returns an opaque key to retrieve them later.</summary>
    Task<string> SaveAsync(byte[] content, string contentType, CancellationToken ct);

    /// <summary>Reads bytes back by key, or null if the key is unknown.</summary>
    Task<StoredFile?> GetAsync(string key, CancellationToken ct);

    /// <summary>Permanently deletes the object for a key. A no-op (never throws) if
    /// the key is unknown or malformed, so it's safe to call while erasing an
    /// account's files best-effort.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Bytes + content type of a stored file.</summary>
public sealed record StoredFile(byte[] Content, string ContentType);
