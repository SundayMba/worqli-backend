using Servika.Application.Abstractions.Persistence;
using Servika.Application.Abstractions.Time;
using Servika.Domain.Notifications;

namespace Servika.Application.Notifications;

/// <summary>Registers (or re-points) a device's Expo push token for the signed-in user.</summary>
public sealed class RegisterPushTokenHandler
{
    private readonly IPushTokenRepository _tokens;
    private readonly IClock _clock;

    public RegisterPushTokenHandler(IPushTokenRepository tokens, IClock clock)
    {
        _tokens = tokens;
        _clock = clock;
    }

    public async Task HandleAsync(Guid userId, string token, string? platform, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var existing = await _tokens.FindByTokenAsync(token.Trim(), ct);
        if (existing is null)
            _tokens.Add(PushToken.Register(userId, token, platform, now));
        else
            existing.Reassign(userId, platform, now); // e.g. same device, new login
        await _tokens.SaveChangesAsync(ct);
    }
}

/// <summary>Removes a device push token (on logout).</summary>
public sealed class RemovePushTokenHandler
{
    private readonly IPushTokenRepository _tokens;

    public RemovePushTokenHandler(IPushTokenRepository tokens)
    {
        _tokens = tokens;
    }

    public async Task HandleAsync(string token, CancellationToken ct)
    {
        await _tokens.RemoveByTokenAsync(token.Trim(), ct);
        await _tokens.SaveChangesAsync(ct);
    }
}
