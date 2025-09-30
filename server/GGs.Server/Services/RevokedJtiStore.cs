using System.Collections.Concurrent;

namespace GGs.Server;

public interface IRevokedJtiStore
{
    void Revoke(string jti, DateTime expiresUtc);
    bool IsRevoked(string jti);
}

public sealed class MemoryRevokedJtiStore : IRevokedJtiStore
{
    private readonly ConcurrentDictionary<string, DateTime> _revoked = new();
    public void Revoke(string jti, DateTime expiresUtc)
    {
        _revoked[jti] = expiresUtc;
        Cleanup();
    }

    public bool IsRevoked(string jti)
    {
        if (_revoked.TryGetValue(jti, out var exp))
        {
            if (DateTime.UtcNow <= exp) return true;
            _revoked.TryRemove(jti, out _);
        }
        return false;
    }

    private void Cleanup()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _revoked)
        {
            if (now > kv.Value) _revoked.TryRemove(kv.Key, out _);
        }
    }
}

