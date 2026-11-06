namespace Servika.Application.Abstractions.Security;

/// <summary>
/// Slows down guessing. Keys are whatever the caller chooses (a normalised email, say) and are counted the same
/// whether or not an account exists, so the throttle itself never tells a stranger that an address is registered.
/// </summary>
public interface IAuthThrottle
{
    /// <summary>True when <paramref name="key"/> has failed too often in the recent window.</summary>
    bool IsBlocked(string key, DateTimeOffset now);

    void RecordFailure(string key, DateTimeOffset now);

    void Reset(string key);

    /// <summary>True the first time in <paramref name="window"/>; false while an earlier claim is still fresh.</summary>
    bool TryClaim(string key, TimeSpan window, DateTimeOffset now);
}
