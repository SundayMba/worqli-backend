using Servika.Application.Abstractions.Persistence;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Lists the jobs assigned to the signed-in artisan, newest first, optionally
/// filtered to one status (e.g. <c>Pending</c> for incoming requests). The
/// artisan mirror of <c>GetMyBookingsHandler</c>. An artisan account with no
/// linked profile simply has no jobs (empty list).
/// </summary>
public sealed class GetArtisanJobsHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;

    public GetArtisanJobsHandler(IBookingRepository bookings, ICatalogueRepository catalogue)
    {
        _bookings = bookings;
        _catalogue = catalogue;
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> HandleAsync(
        Guid artisanUserId, string? status, CancellationToken ct)
    {
        var profile = await _catalogue.GetArtisanByUserIdAsync(artisanUserId, ct);
        if (profile is null)
            return Array.Empty<BookingSummaryDto>();

        BookingStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
                throw new ArgumentException($"Unknown booking status '{status}'.", nameof(status));
            filter = parsed;
        }

        var jobs = await _bookings.ListForArtisanAsync(profile.Id, filter, ct);
        return jobs.Select(b => b.ToSummaryDto()).ToList();
    }
}
