using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Servika.Application.Common;
using Servika.Application.Tracking;

namespace Servika.Api.Hubs;

/// <summary>
/// Real-time location tracking for a booking's trip. Clients connect with their
/// JWT (passed as the <c>access_token</c> query param — WebSockets can't send the
/// Authorization header) and join a per-booking group; the assigned artisan pushes
/// location pings which are broadcast to everyone watching that booking.
///
/// Client → server: <c>JoinBookingTracking</c>, <c>LeaveBookingTracking</c>,
/// <c>SendLocationUpdate</c>. Server → client: <c>TrackingStarted</c>,
/// <c>LocationUpdated</c>, <c>TrackingEnded</c>, <c>TrackingError</c>.
///
/// All authorisation/state rules live in <see cref="TrackingService"/>; the hub
/// just maps connections to groups and turns rejections into <c>TrackingError</c>.
/// </summary>
[Authorize]
public sealed class TrackingHub : Hub
{
    private readonly TrackingService _tracking;

    public TrackingHub(TrackingService tracking)
    {
        _tracking = tracking;
    }

    private static string GroupName(Guid bookingId) => $"booking:{bookingId}";

    /// <summary>Join a booking's tracking group (customer-owner or assigned artisan only).</summary>
    public async Task JoinBookingTracking(Guid bookingId)
    {
        try
        {
            await _tracking.AuthorizeJoinAsync(CurrentUserId(), IsArtisan(), bookingId, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(bookingId));
            await Clients.Caller.SendAsync("TrackingStarted", new { bookingId });
        }
        catch (Exception ex)
        {
            await SendError(ex);
        }
    }

    /// <summary>Leave a booking's tracking group.</summary>
    public Task LeaveBookingTracking(Guid bookingId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(bookingId));

    /// <summary>Push the artisan's current position. Validated server-side (assigned
    /// artisan, booking OnMyWay, valid coordinates) then broadcast to the group.</summary>
    public async Task SendLocationUpdate(
        Guid bookingId, double latitude, double longitude,
        double? accuracy, double? heading, double? speed)
    {
        try
        {
            var result = await _tracking.RecordLocationAsync(
                CurrentUserId(), bookingId, latitude, longitude, accuracy, heading, speed,
                Context.ConnectionAborted);

            var group = GroupName(bookingId);
            if (result.Started)
                await Clients.Group(group).SendAsync("TrackingStarted", new { bookingId });
            await Clients.Group(group).SendAsync("LocationUpdated", result.Update);
        }
        catch (Exception ex)
        {
            await SendError(ex);
        }
    }

    // Known, caller-safe failures carry their message; anything else is masked.
    private Task SendError(Exception ex)
    {
        var message = ex is TrackingNotAllowedException or ArgumentException
            ? ex.Message
            : "Tracking error.";
        return Clients.Caller.SendAsync("TrackingError", new { message });
    }

    private bool IsArtisan() => Context.User?.IsInRole("Artisan") ?? false;

    private Guid CurrentUserId()
    {
        var sub = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var userId))
            throw new TrackingNotAllowedException("Not signed in.");

        return userId;
    }
}
