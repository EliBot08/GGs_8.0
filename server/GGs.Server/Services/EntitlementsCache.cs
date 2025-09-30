using Microsoft.Extensions.Caching.Memory;

namespace GGs.Server.Services;

public interface IEntitlementsCache
{
    bool TryGet(string userId, string[] roles, out GGs.Shared.Api.Entitlements value);
    void Set(string userId, string[] roles, GGs.Shared.Api.Entitlements value, TimeSpan ttl);
}

public sealed class MemoryEntitlementsCache : IEntitlementsCache
{
    private readonly IMemoryCache _cache;
    public MemoryEntitlementsCache(IMemoryCache cache) { _cache = cache; }
    private static string MakeKey(string userId, string[] roles)
        => $"ent::{userId}::{string.Join('|', (roles ?? Array.Empty<string>()).Order(StringComparer.OrdinalIgnoreCase))}";
    public bool TryGet(string userId, string[] roles, out GGs.Shared.Api.Entitlements value)
    {
        return _cache.TryGetValue(MakeKey(userId, roles), out value!);
    }
    public void Set(string userId, string[] roles, GGs.Shared.Api.Entitlements value, TimeSpan ttl)
    {
        _cache.Set(MakeKey(userId, roles), value, ttl);
    }
}


