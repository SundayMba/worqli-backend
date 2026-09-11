using Microsoft.EntityFrameworkCore;
using Servika.Application.Abstractions.Persistence;
using Servika.Domain.Payments;

namespace Servika.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAccountEraser"/>. Collects every storage key
/// the account owns, then deletes all its rows in one transaction. Rows that cascade
/// from the user's foreign keys go automatically when the user row is deleted; the
/// rest — data keyed by the artisan's <b>profile id</b> or by <b>owner id</b> with no
/// cascading FK (assigned bookings, the wallet ledger, reviews/bids received, others'
/// favourites of this artisan, artisan-side conversations, referrals they made) — are
/// deleted explicitly first.
/// </summary>
public sealed class AccountEraser : IAccountEraser
{
    private readonly ServikaDbContext _db;

    public AccountEraser(ServikaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> EraseAsync(Guid userId, CancellationToken ct)
    {
        // The artisan profile (if any) whose id keys most of the un-cascaded data.
        // IgnoreQueryFilters throughout: the purge worker erases SOFT-DELETED accounts,
        // which the global filters would otherwise hide from these queries.
        var profile = await _db.ArtisanProfiles.AsNoTracking().IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
        var profileId = profile?.Id;

        // ── 1. Collect every storage key the account owns (before deleting rows). ──
        var keys = new List<string>();

        if (profile is not null)
        {
            if (!string.IsNullOrWhiteSpace(profile.PhotoKey)) keys.Add(profile.PhotoKey);
            if (!string.IsNullOrWhiteSpace(profile.CoverPhotoKey)) keys.Add(profile.CoverPhotoKey);
            if (!string.IsNullOrWhiteSpace(profile.CertificateKey)) keys.Add(profile.CertificateKey);
            keys.AddRange(profile.GalleryPhotoKeys);
        }

        var kycKeys = await _db.ArtisanKycSubmissions.AsNoTracking()
            .Where(k => k.UserId == userId)
            .Select(k => new { k.SelfieKey, k.IdDocumentKey, k.PoseSelfiesJson })
            .ToListAsync(ct);
        foreach (var k in kycKeys)
        {
            if (!string.IsNullOrWhiteSpace(k.SelfieKey)) keys.Add(k.SelfieKey);
            if (!string.IsNullOrWhiteSpace(k.IdDocumentKey)) keys.Add(k.IdDocumentKey);
            if (string.IsNullOrWhiteSpace(k.PoseSelfiesJson)) continue;
            try
            {
                foreach (var p in System.Text.Json.JsonSerializer.Deserialize<List<Servika.Application.Catalogue.PoseShot>>(k.PoseSelfiesJson) ?? new())
                    if (!string.IsNullOrWhiteSpace(p.Key)) keys.Add(p.Key);
            }
            catch (System.Text.Json.JsonException) { /* unreadable list: nothing more to delete */ }
        }

        // Every booking the account touches — as customer OR (if artisan) as the
        // assigned artisan — carries media we must remove.
        var bookingMedia = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == userId || (profileId != null && b.ArtisanId == profileId))
            .Select(b => new { b.MediaKeys, b.VideoKey, b.CompletionPhotoKeys, b.CompletionReceiptKey })
            .ToListAsync(ct);
        foreach (var b in bookingMedia)
        {
            keys.AddRange(b.MediaKeys);
            if (!string.IsNullOrWhiteSpace(b.VideoKey)) keys.Add(b.VideoKey);
            keys.AddRange(b.CompletionPhotoKeys);
            if (b.CompletionReceiptKey is not null) keys.Add(b.CompletionReceiptKey);
        }

        // ── 2. Delete rows in one transaction. ──
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Ledger + payments owned by this user or this artisan profile (no cascade).
        await _db.WalletTransactions
            .Where(w => w.OwnerId == userId || (profileId != null && w.OwnerId == profileId))
            .ExecuteDeleteAsync(ct);
        await _db.Payments.Where(p => p.CustomerId == userId).ExecuteDeleteAsync(ct);

        if (profileId is { } pid)
        {
            // Data keyed by the artisan's profile id, on bookings that survive.
            await _db.Bids.Where(b => b.ArtisanId == pid).ExecuteDeleteAsync(ct);
            await _db.Reviews.Where(r => r.ArtisanId == pid).ExecuteDeleteAsync(ct);
            await _db.Favorites.Where(f => f.ArtisanId == pid).ExecuteDeleteAsync(ct);
            // Jobs assigned to this artisan (raised by other customers) + their
            // payments/bids/reviews/disputes/tracking via the booking FK cascade.
            await _db.Bookings.Where(b => b.ArtisanId == pid).ExecuteDeleteAsync(ct);
            // The profile itself (artisan_services cascade from it).
            await _db.ArtisanProfiles.IgnoreQueryFilters().Where(p => p.Id == pid).ExecuteDeleteAsync(ct);
        }

        // Conversations where this user is the ARTISAN side (customer side cascades
        // from the user delete below); messages cascade from the conversation.
        await _db.Conversations.Where(c => c.ArtisanUserId == userId).ExecuteDeleteAsync(ct);
        // Referrals this user MADE (the referred side cascades from the user delete).
        await _db.Referrals.Where(r => r.ReferrerUserId == userId).ExecuteDeleteAsync(ct);

        // Finally the user: cascades refresh tokens, verification codes, push tokens,
        // the user's own favourites, notifications, customer bookings (+ children),
        // withdrawals, KYC, referred-referrals, and customer conversations (+ messages).
        await _db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);

        return keys;
    }
}
