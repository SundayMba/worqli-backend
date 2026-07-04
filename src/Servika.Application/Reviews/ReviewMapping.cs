using Servika.Contracts.Reviews;
using Servika.Domain.Reviews;

namespace Servika.Application.Reviews;

/// <summary>
/// Maps the <see cref="Review"/> Domain entity to its public Contracts DTO. Kept
/// in one place so handlers stay focused on the use case.
/// </summary>
internal static class ReviewMapping
{
    public static ReviewDto ToDto(this Review r) =>
        new(r.Id, r.BookingId, r.ArtisanId, r.CustomerName, r.Rating, r.Comment,
            r.ServiceName, r.CreatedAt);
}
