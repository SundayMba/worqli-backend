using Microsoft.Extensions.Configuration;
using Servika.Application.Abstractions.Storage;

namespace Servika.Infrastructure.Storage;

/// <summary>
/// Dev/launch <see cref="IFileStorage"/> that writes bytes to a local folder and
/// returns the filename as the key. Swappable for an S3-backed implementation on
/// deploy (same port). Folder is <c>Storage:LocalPath</c> or <c>&lt;base&gt;/uploads</c>.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IConfiguration configuration)
    {
        var configured = configuration["Storage:LocalPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "uploads")
            : configured;
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(byte[] content, string contentType, CancellationToken ct)
    {
        var key = $"{Guid.NewGuid():N}{ExtFor(contentType)}";
        await File.WriteAllBytesAsync(Path.Combine(_root, key), content, ct);
        return key;
    }

    public async Task<StoredFile?> GetAsync(string key, CancellationToken ct)
    {
        // Guard against path traversal — keys are bare filenames.
        if (string.IsNullOrWhiteSpace(key) || key.Contains('/') || key.Contains('\\'))
            return null;

        var path = Path.Combine(_root, key);
        if (!File.Exists(path)) return null;

        var bytes = await File.ReadAllBytesAsync(path, ct);
        return new StoredFile(bytes, ContentTypeFor(key));
    }

    private static string ExtFor(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        "video/mp4" => ".mp4",
        _ => ".jpg",
    };

    private static string ContentTypeFor(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        _ => "image/jpeg",
    };
}
