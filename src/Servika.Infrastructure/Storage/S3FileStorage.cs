using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Servika.Application.Abstractions.Storage;

namespace Servika.Infrastructure.Storage;

/// <summary>
/// Production <see cref="IFileStorage"/> backed by Amazon S3 — uploads (KYC
/// documents, profile/cover/gallery photos, booking media) survive redeploys
/// and work across instances, unlike <see cref="LocalFileStorage"/>'s disk
/// folder. Selected automatically when <c>Storage:S3Bucket</c> is configured.
///
/// Credentials come from the AWS SDK's default chain: on EC2 that's the
/// instance's IAM role (no keys in config at all — the recommended setup);
/// locally it would be AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY env vars.
/// The bucket stays fully private — the API streams bytes to clients itself,
/// so objects never need public URLs.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3FileStorage(IConfiguration configuration)
    {
        _bucket = configuration["Storage:S3Bucket"]
            ?? throw new InvalidOperationException("Storage:S3Bucket is not configured.");

        var region = configuration["Storage:S3Region"];
        _s3 = string.IsNullOrWhiteSpace(region)
            ? new AmazonS3Client() // region from the environment / instance metadata
            : new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
    }

    public async Task<string> SaveAsync(byte[] content, string contentType, CancellationToken ct)
    {
        // Same key shape as LocalFileStorage, so keys written locally and keys
        // written to S3 are interchangeable strings from the app's viewpoint.
        var key = $"{Guid.NewGuid():N}{ExtFor(contentType)}";
        using var stream = new MemoryStream(content);
        await _s3.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
            },
            ct);
        return key;
    }

    public async Task<StoredFile?> GetAsync(string key, CancellationToken ct)
    {
        // Keys are bare filenames; anything path-like is not ours.
        if (string.IsNullOrWhiteSpace(key) || key.Contains('/') || key.Contains('\\'))
            return null;

        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, key, ct);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            return new StoredFile(
                buffer.ToArray(),
                string.IsNullOrWhiteSpace(response.Headers.ContentType)
                    ? ContentTypeFor(key)
                    : response.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('/') || key.Contains('\\'))
            return;

        try
        {
            await _s3.DeleteObjectAsync(_bucket, key, ct);
        }
        catch (AmazonS3Exception)
        {
            // Best-effort: a failed object delete must not abort an account erase.
        }
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
