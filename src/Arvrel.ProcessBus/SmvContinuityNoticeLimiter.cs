using System.Collections.Concurrent;

namespace Arvrel.ProcessBus;

internal sealed class SmvContinuityNoticeLimiter
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRaised = new(StringComparer.Ordinal);
    private readonly TimeSpan _interval;

    public SmvContinuityNoticeLimiter(TimeSpan? interval = null)
    {
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    public bool ShouldRaise(string streamKey, string code, DateTimeOffset now)
    {
        var key = $"{streamKey}|{code}";
        while (true)
        {
            if (!_lastRaised.TryGetValue(key, out var previous))
            {
                if (_lastRaised.TryAdd(key, now))
                    return true;
                continue;
            }

            if (now - previous < _interval)
                return false;

            if (_lastRaised.TryUpdate(key, now, previous))
                return true;
        }
    }

    public void Clear() => _lastRaised.Clear();
}
