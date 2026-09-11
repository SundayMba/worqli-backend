using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Catalogue;
using Servika.Domain.Catalogue;

namespace Servika.Application.Catalogue;

internal static class VerificationEventMapping
{
    /// <summary>Artisan-facing: the reviewer is never named.</summary>
    public static IReadOnlyList<VerificationEventDto> ToArtisanDtos(this IEnumerable<VerificationEvent> events) =>
        events.Select(e => new VerificationEventDto(
            e.Id, e.Action.ToString(), e.Check?.ToString(), e.ReasonCode, e.Note,
            e.ActorUserId is null ? "You" : "Reviewer", e.CreatedAtUtc)).ToList();

    /// <summary>Admin-facing: names resolved.</summary>
    public static async Task<IReadOnlyList<VerificationEventDto>> ToAdminDtosAsync(
        this IEnumerable<VerificationEvent> events, IUserRepository users, string artisanName, CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        var list = new List<VerificationEventDto>();
        foreach (var e in events)
        {
            string actor;
            if (e.ActorUserId is null) actor = artisanName;
            else if (!names.TryGetValue(e.ActorUserId.Value, out actor!))
            {
                actor = (await users.FindByIdAsync(e.ActorUserId.Value, ct))?.FullName ?? "Reviewer";
                names[e.ActorUserId.Value] = actor;
            }
            list.Add(new VerificationEventDto(e.Id, e.Action.ToString(), e.Check?.ToString(), e.ReasonCode, e.Note, actor, e.CreatedAtUtc));
        }
        return list;
    }

    public static VerificationCheck ParseCheck(string? check)
    {
        if (!Enum.TryParse<VerificationCheck>(check, ignoreCase: true, out var c))
            throw new ArgumentException($"Unknown check '{check}'. Use Trade, Photo, Identity, Selfie, Guarantors, Payout or Documents.");
        return c;
    }
}
