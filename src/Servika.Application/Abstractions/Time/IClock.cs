namespace Servika.Application.Abstractions.Time;

/// <summary>
/// Supplies the current time. The use cases depend on this instead of calling
/// DateTimeOffset.UtcNow directly, so a test can freeze "now" and assert on
/// exact expiry timestamps. Infrastructure provides the real (system-clock) one.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
