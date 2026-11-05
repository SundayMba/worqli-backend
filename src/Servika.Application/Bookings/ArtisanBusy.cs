using Servika.Application.Abstractions.Persistence;
using Servika.Application.Common;

namespace Servika.Application.Bookings;

/// <summary>
/// An artisan is busy while one of their jobs is Arrived or In progress: they are standing in someone's home.
/// A busy artisan still sees and may bid on every open request, urgent ones included, but the offer must say when
/// they will be free. The customer sees "On a job now · free from 3:30pm" beside the other offers and decides.
/// (On the way does not count: they can still finish the trip and plan the next job.)
/// A job left at Arrived for <see cref="ArrivedCountsFor"/>, or In progress for <see cref="WorkingCountsFor"/>, stops
/// counting: someone forgot to move it on, and it must not mark them busy for ever.
/// </summary>
public static class ArtisanBusy
{
    public static readonly TimeSpan ArrivedCountsFor = TimeSpan.FromHours(12);
    public static readonly TimeSpan WorkingCountsFor = TimeSpan.FromHours(24);

    public const string NeedsTime = "You are on a job right now. Say when you will be free.";

    public static string CapReached(int max) =>
        $"You have {max} {(max == 1 ? "job" : "jobs")} on the go, the most you can hold at once. Finish one before taking another.";

    public static string CapReachedForCustomer(string artisanName) =>
        $"{artisanName.Split(' ')[0]} has as many jobs as they can take right now. Pick another offer.";
}

/// <summary>The cap on accepted, unfinished jobs one artisan may hold (<c>PlatformSettings.MaxActiveJobs</c>).</summary>
public sealed class ArtisanCapacity
{
    private readonly IBookingRepository _bookings;
    private readonly IPlatformSettingsRepository _settings;

    public ArtisanCapacity(IBookingRepository bookings, IPlatformSettingsRepository settings)
    {
        _bookings = bookings;
        _settings = settings;
    }

    /// <summary>Throws a 409 when the artisan already holds the most jobs allowed. <paramref name="forCustomerName"/>
    /// set = the customer is the one picking, so the message speaks to them.</summary>
    public async Task EnsureRoomAsync(Guid artisanProfileId, CancellationToken ct, string? forCustomerName = null)
    {
        var max = (await _settings.GetOrCreateAsync(ct)).MaxActiveJobs;
        if (await _bookings.CountActiveForArtisanAsync(artisanProfileId, ct) < max) return;
        throw new ConflictException(forCustomerName is null ? ArtisanBusy.CapReached(max) : ArtisanBusy.CapReachedForCustomer(forCustomerName));
    }
}
