using System.Collections.Concurrent;

namespace Ukweli.Api.Auth;

/// <summary>
/// A per-IP fixed-window limit on how often a claim may be analysed.
/// </summary>
/// <remarks>
/// <para>
/// In memory, because the demo is a single process. That is a real limitation
/// and worth naming: behind more than one instance each would keep its own
/// count, so the effective limit would multiply. A shared store is the fix, and
/// it is not warranted for a single container.
/// </para>
/// <para>
/// The limit applies whether or not the caller is signed in. Each analysis
/// costs a model call, and requiring a session to spend that would only mean an
/// attacker signs in first.
/// </para>
/// </remarks>
public sealed class RateLimiter(UkweliOptions options, TimeProvider? timeProvider = null)
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private sealed record Window(DateTimeOffset StartedAt, int Count);

    /// <summary>Whether this caller may proceed, counting the attempt if so.</summary>
    public bool TryAcquire(string clientKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientKey);

        var now = _time.GetUtcNow();
        var allowed = true;

        _windows.AddOrUpdate(
            clientKey,
            _ => new Window(now, 1),
            (_, existing) =>
            {
                if (now - existing.StartedAt >= TimeSpan.FromMinutes(1))
                {
                    return new Window(now, 1);
                }

                if (existing.Count >= options.RateLimitAnalyzePerMinute)
                {
                    allowed = false;
                    return existing;
                }

                return existing with { Count = existing.Count + 1 };
            });

        // Bounded so a flood of distinct addresses cannot grow this without limit.
        if (_windows.Count > 10_000)
        {
            Prune(now);
        }

        return allowed;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var (key, window) in _windows)
        {
            if (now - window.StartedAt >= TimeSpan.FromMinutes(1))
            {
                _windows.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Identifies the caller. Behind Caddy the socket address is the proxy, so
    /// the forwarded address is used when one is present.
    /// </summary>
    public static string ClientKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded) &&
            forwarded.Count > 0 &&
            forwarded[0] is { Length: > 0 } value)
        {
            // The left-most entry is the original client.
            return value.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
