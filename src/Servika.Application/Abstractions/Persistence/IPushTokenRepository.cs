using Servika.Domain.Notifications;

namespace Servika.Application.Abstractions.Persistence;

/// <summary>Persistence for device push tokens.</summary>
public interface IPushTokenRepository
{
    void Add(PushToken token);

    /// <summary>An existing registration for this token string, or null (tracked, so
    /// re-registering can re-point it to the current user).</summary>
    Task<PushToken?> FindByTokenAsync(string token, CancellationToken ct);

    /// <summary>Every push token registered to a user (all their devices).</summary>
    Task<IReadOnlyList<PushToken>> ListForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>Removes a token registration (on logout / when Expo reports it dead).</summary>
    Task RemoveByTokenAsync(string token, CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}
