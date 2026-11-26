using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace DemoMVC.Services;

public sealed class EmailRequestLimiter : IEmailRequestLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private const int MaximumRequests = 5;
    private const int MaximumTrackedKeys = 10000;

    private readonly ConcurrentDictionary<string, WindowState> _windows = new();
    private int _calls;

    public bool TryAcquire(string? email, string? clientAddress)
    {
        var now = DateTimeOffset.UtcNow;
        PruneExpiredEntries(now);

        if (_windows.Count >= MaximumTrackedKeys)
        {
            return false;
        }

        var addressKey = CreateKey("ip", clientAddress);
        if (!TryAcquireKey(addressKey, now))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        return TryAcquireKey(CreateKey("email", email.Trim().ToUpperInvariant()), now);
    }

    private bool TryAcquireKey(string key, DateTimeOffset now)
    {
        while (true)
        {
            if (!_windows.TryGetValue(key, out var current))
            {
                if (_windows.TryAdd(key, new WindowState(now, 1)))
                {
                    return true;
                }

                continue;
            }

            if (current.StartedAt + Window <= now)
            {
                if (_windows.TryUpdate(key, new WindowState(now, 1), current))
                {
                    return true;
                }

                continue;
            }

            if (current.Count >= MaximumRequests)
            {
                return false;
            }

            if (_windows.TryUpdate(key, current with { Count = current.Count + 1 }, current))
            {
                return true;
            }
        }
    }

    private void PruneExpiredEntries(DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _calls) % 128 != 0 && _windows.Count < MaximumTrackedKeys)
        {
            return;
        }

        foreach (var entry in _windows)
        {
            if (entry.Value.StartedAt + Window <= now &&
                _windows.TryGetValue(entry.Key, out var current) &&
                ReferenceEquals(entry.Value, current))
            {
                _windows.TryRemove(entry.Key, out _);
            }
        }
    }

    private static string CreateKey(string category, string? value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return $"{category}:{Convert.ToHexString(hash)}";
    }

    private sealed record WindowState(DateTimeOffset StartedAt, int Count);
}
