using Servika.Application.Abstractions.Time;

namespace Servika.Infrastructure.Time;

/// <summary>The real clock, backed by the system UTC time.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
