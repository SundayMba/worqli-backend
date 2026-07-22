using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Storage;
using Servika.Application.Abstractions.Time;
using Servika.Application.Common;
using Servika.Contracts.Bookings;
using Servika.Domain.Bookings;

namespace Servika.Application.Bookings;

/// <summary>
/// Submits a new booking for the signed-in customer. Validates the chosen
/// category (and artisan, if one was pre-selected) against the catalogue, then
/// records the booking in <see cref="BookingStatus.Pending"/>.
///
/// Pricing is decided here, never trusted from the client: we default to
/// <see cref="PricingModel.Variable"/> (most home-repair jobs), carry the chosen
/// artisan's inspection fee as the up-front amount, and stamp the commission rate
/// from the admin-controlled <see cref="Servika.Domain.Settings.PlatformSettings"/>
/// (standard vs emergency by urgency) so monetisation is a settings change, not a
/// redeploy. Defaults to 0 during the launch window.
/// </summary>
public sealed class CreateBookingHandler
{
    private readonly IBookingRepository _bookings;
    private readonly ICatalogueRepository _catalogue;
    private readonly IArtisanServiceRepository _artisanServices;
    private readonly IUserRepository _users;
    private readonly IPlatformSettingsRepository _settings;
    private readonly AuthPolicyOptions _authPolicy;
    private readonly Notifications.NotificationEmitter _notifications;
    private readonly IFileStorage _files;
    private readonly IClock _clock;

    public CreateBookingHandler(
        IBookingRepository bookings,
        ICatalogueRepository catalogue,
        IArtisanServiceRepository artisanServices,
        IUserRepository users,
        IPlatformSettingsRepository settings,
        AuthPolicyOptions authPolicy,
        Notifications.NotificationEmitter notifications,
        IFileStorage files,
        IClock clock)
    {
        _bookings = bookings;
        _catalogue = catalogue;
        _artisanServices = artisanServices;
        _users = users;
        _settings = settings;
        _authPolicy = authPolicy;
        _notifications = notifications;
        _files = files;
        _clock = clock;
    }

    public async Task<BookingDetailDto> HandleAsync(
        Guid customerId, CreateBookingRequest request, CancellationToken ct)
    {
        // Phone-verification gate (off by default; a config flip turns it on once
        // the apps prompt). A reachable number matters here — the artisan calls it.
        if (_authPolicy.RequirePhoneForBooking)
        {
            var customer = await _users.FindByIdAsync(customerId, ct);
            if (customer is { IsPhoneVerified: false })
                throw new PhoneVerificationRequiredException();
        }

        var slug = request.CategorySlug?.Trim() ?? string.Empty;
        var category = await _catalogue.GetCategoryBySlugAsync(slug, ct)
            ?? throw new NotFoundException($"Category '{slug}' was not found.");

        string? artisanName = null;
        if (request.ArtisanId is { } artisanId)
        {
            var artisan = await _catalogue.GetArtisanByIdAsync(artisanId, ct)
                ?? throw new NotFoundException($"Artisan '{artisanId}' was not found.");
            artisanName = artisan.FullName;
        }

        // A published fixed-price service: the price is known before booking —
        // read from the artisan's own listing, never from the client. The
        // customer pays the moment the artisan accepts (no quote round-trip).
        Domain.Catalogue.ArtisanService? fixedService = null;
        if (request.ArtisanServiceId is { } serviceId)
        {
            if (request.ArtisanId is null)
                throw new ArgumentException("A fixed-price service needs its artisan selected.");
            fixedService = await _artisanServices.FindAsync(serviceId, ct);
            if (fixedService is null || fixedService.ArtisanProfileId != request.ArtisanId)
                throw new NotFoundException("That service was not found on this artisan's profile.");
        }

        // Commission comes from admin settings — emergency rate for urgent jobs.
        var urgency = ParseUrgency(request.Urgency);
        var settings = await _settings.GetOrCreateAsync(ct);
        var commissionRate = urgency == Urgency.Urgent
            ? settings.EmergencyCommissionRate
            : settings.CommissionRate;

        // Job media: photos (context for any artisan) + an optional short video.
        // Stored before the row so a bad upload fails clean.
        var assessment = ParseAssessment(request.AssessmentMode);
        var mediaKeys = new List<string>();
        foreach (var photo in (request.MediaBase64 ?? new()).Take(4))
        {
            var bytes = DecodeMedia(photo, "job photo");
            mediaKeys.Add(await _files.SaveAsync(bytes, "image/jpeg", ct));
        }
        string? videoKey = null;
        if (!string.IsNullOrWhiteSpace(request.VideoBase64))
        {
            var bytes = DecodeMedia(request.VideoBase64, "job video");
            videoKey = await _files.SaveAsync(bytes, "video/mp4", ct);
        }
        // Bidding needs something to assess — require at least one photo or a video.
        if (assessment == AssessmentMode.RemoteQuote &&
            request.ArtisanId is null &&
            mediaKeys.Count == 0 && videoKey is null)
        {
            throw new ArgumentException(
                "Add at least one photo (or a short video) of the job so artisans can price it.");
        }

        var booking = Booking.Create(
            customerId: customerId,
            artisanId: request.ArtisanId,
            categorySlug: category.Slug,
            // A fixed-price booking is FOR that listed service — its name is the
            // one that should show everywhere ("Knotless braids", not "Beauty").
            serviceName: fixedService?.Name ?? category.Name,
            artisanName: artisanName,
            description: request.Description,
            addressText: request.AddressText,
            locationLat: request.LocationLat,
            locationLng: request.LocationLng,
            locationInstructions: request.LocationInstructions,
            preferredDate: request.PreferredDate,
            preferredTimeSlot: request.PreferredTimeSlot,
            urgency: urgency,
            // Fixed = a published price known at booking; Variable = the price is
            // agreed later via a quote. Booking itself is always free either way —
            // payment for a fixed booking happens once the artisan accepts.
            pricingModel: fixedService is null ? PricingModel.Variable : PricingModel.Fixed,
            initialQuoteAmountNaira: fixedService?.PriceNaira,
            commissionRate: commissionRate,
            now: _clock.UtcNow,
            assessment: assessment,
            mediaKeys: mediaKeys,
            videoKey: videoKey);

        _bookings.Add(booking);
        if (booking.ArtisanId is null)
            // Open request — broadcast to every matching artisan so one can claim it.
            await _notifications.OpenJobPosted(booking, ct);
        else
            // Pre-selected — let the assigned artisan know a job came in.
            await _notifications.ArtisanNewBooking(booking, ct);
        await _bookings.SaveChangesAsync(ct);

        return booking.ToDetailDto();
    }

    private static AssessmentMode ParseAssessment(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "remotequote" or "remote" or "bidding" => AssessmentMode.RemoteQuote,
            "inspection" or null or "" => AssessmentMode.Inspection,
            _ => throw new ArgumentException(
                "AssessmentMode must be 'Inspection' or 'RemoteQuote'.", nameof(value)),
        };

    private static byte[] DecodeMedia(string base64, string label)
    {
        var comma = base64.IndexOf(',');
        var payload = base64.StartsWith("data:") && comma >= 0 ? base64[(comma + 1)..] : base64;
        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length == 0) throw new ArgumentException($"The {label} is empty.");
            return bytes;
        }
        catch (FormatException)
        {
            throw new ArgumentException($"The {label} is not valid base64.");
        }
    }

    private static Urgency ParseUrgency(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "urgent" => Urgency.Urgent,
            "standard" or null or "" => Urgency.Standard,
            _ => throw new ArgumentException(
                "Urgency must be 'standard' or 'urgent'.", nameof(value)),
        };
}
