using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

/// <summary>
/// NDPA retention for verification selfies: once a reviewer has decided, the straight
/// selfie and every pose selfie are deleted (24h after approval, 30 days after a
/// decline, since a declined artisan may send a fix back). The ID document image and
/// the public profile photo are kept. File deletes are best effort; the keys are
/// cleared and the time stamped so the admin file shows "Selfies deleted".
/// </summary>
public sealed class KycImageRetentionHandler
{
    private const int BatchSize = 200;

    private readonly IArtisanKycRepository _kyc;
    private readonly IFileStorage _storage;

    public KycImageRetentionHandler(IArtisanKycRepository kyc, IFileStorage storage)
    {
        _kyc = kyc;
        _storage = storage;
    }

    /// <summary>Deletes due selfies and returns how many applications were cleaned.</summary>
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var due = await _kyc.ListSelfieRetentionDueAsync(
            now - ArtisanKyc.VerifiedSelfieRetention, now - ArtisanKyc.RejectedSelfieRetention, BatchSize, ct);
        var cleaned = 0;
        foreach (var k in due)
        {
            if (!k.IsSelfieDeletionDue(now)) continue;
            foreach (var key in SelfieKeys(k))
                await _storage.DeleteAsync(key, ct);
            k.MarkSelfieImagesDeleted(now);
            cleaned++;
        }
        if (cleaned > 0) await _kyc.SaveChangesAsync(ct);
        return cleaned;
    }

    /// <summary>The straight selfie plus every pose selfie key on the application.</summary>
    public static IReadOnlyList<string> SelfieKeys(ArtisanKyc k)
    {
        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(k.SelfieKey)) keys.Add(k.SelfieKey);
        if (!string.IsNullOrWhiteSpace(k.PoseSelfiesJson))
        {
            try
            {
                var shots = System.Text.Json.JsonSerializer.Deserialize<List<PoseShot>>(k.PoseSelfiesJson);
                keys.AddRange((shots ?? new()).Select(s => s.Key).Where(x => !string.IsNullOrWhiteSpace(x)));
            }
            catch (System.Text.Json.JsonException) { /* unreadable list: nothing more to delete */ }
        }
        return keys;
    }
}
