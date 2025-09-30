using System.Collections.Concurrent;

namespace GGs.Server.Services;

public sealed class DeviceRegistry
{
    private sealed record Entry(string ConnectionId, DateTime LastHeartbeatUtc);
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public void Register(string deviceId, string connectionId)
        => _entries[deviceId] = new Entry(connectionId, DateTime.UtcNow);

    public void Heartbeat(string deviceId)
    {
        _entries.AddOrUpdate(deviceId,
            id => new Entry("", DateTime.UtcNow),
            (id, existing) => existing with { LastHeartbeatUtc = DateTime.UtcNow });
    }

    public void UnregisterByConnection(string connectionId)
    {
        foreach (var kv in _entries)
        {
            if (kv.Value.ConnectionId == connectionId)
            {
                _entries.TryRemove(kv.Key, out _);
                return;
            }
        }
    }

    public string? GetConnection(string deviceId)
        => _entries.TryGetValue(deviceId, out var e) ? e.ConnectionId : null;

    public IReadOnlyCollection<string> GetDevices() => _entries.Keys.ToArray();

    public int ExpireStale(TimeSpan maxAge)
    {
        var now = DateTime.UtcNow;
        var removed = 0;
        foreach (var kv in _entries)
        {
            if (now - kv.Value.LastHeartbeatUtc > maxAge)
            {
                if (_entries.TryRemove(kv.Key, out _)) removed++;
            }
        }
        return removed;
    }
}
