using Servika.Contracts.Disputes;
using Servika.Domain.Disputes;

namespace Servika.Application.Disputes;

/// <summary>Maps a dispute Domain entity to its public DTO.</summary>
internal static class DisputeMapping
{
    public static DisputeDto ToDto(this Dispute d) =>
        new(d.Id, d.BookingId, d.CustomerName, d.ServiceName, d.Category, d.Description,
            d.Status.ToString(), d.Resolution.ToString(), d.ResolutionNote,
            d.CreatedAt, d.ResolvedAtUtc);
}
