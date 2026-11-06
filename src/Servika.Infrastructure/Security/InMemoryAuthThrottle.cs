using System.Collections.Concurrent;
using Servika.Application.Abstractions.Security;

namespace Servika.Infrastructure.Security;

/// <summary>
/// Process-local throttle (one API instance today). Ten failures in fifteen minutes blocks a key until the oldest
/// failure ages out. Swap for a Redis-backed one when the API runs on more than one instance.
/// </summary>
public sealed class InMemoryAuthThrottle : IAuthThrottle
{
    public const int MaxFailures = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> _failures = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _claims = new();

    public bool IsBlocked(string key, DateTimeOffset now)
    {
        if (!_failures.TryGetValue(key, out var q)) return false;
        lock (q)
        {
            Prune(q, now);
            return q.Count >= MaxFailures;
        }
    }

    public void RecordFailure(string key, DateTimeOffset now)
    {
        var q = _failures.GetOrAdd(key, _ => new Queue<DateTimeOffset>());
        lock (q)
        {
            Prune(q, now);
            q.Enqueue(now);
        }
        if (_failures.Count > 50_000) Sweep(now);
    }

    public void Reset(string key) => _failures.TryRemove(key, out _);

    public bool TryClaim(string key, TimeSpan window, DateTimeOffset now)
    {
        var claimed = false;
        _claims.AddOrUpdate(key,
            _ => { claimed = true; return now; },
            (_, last) => { if (now - last < window) return last; claimed = true; return now; });
        if (_claims.Count > 50_000)
            foreach (var kv in _claims) if (now - kv.Value > TimeSpan.FromDays(1)) _claims.TryRemove(kv.Key, out _);
        return claimed;
    }

    private static void Prune(Queue<DateTimeOffset> q, DateTimeOffset now)
    {
        while (q.Count > 0 && now - q.Peek() > Window) q.Dequeue();
    }

    private void Sweep(DateTimeOffset now)
    {
        foreach (var kv in _failures)
            lock (kv.Value)
            {
                Prune(kv.Value, now);
                if (kv.Value.Count == 0) _failures.TryRemove(kv.Key, out _);
            }
    }
}
