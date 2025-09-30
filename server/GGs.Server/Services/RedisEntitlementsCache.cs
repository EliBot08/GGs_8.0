using GGs.Shared.Api;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace GGs.Server.Services;

public interface IDistributedEntitlementsCache
{
    Task<EntitlementsResponse?> GetAsync(string userId);
    Task SetAsync(string userId, EntitlementsResponse entitlements, TimeSpan? expiry = null);
    Task RemoveAsync(string userId);
    Task RemoveByPatternAsync(string pattern);
}

public sealed class RedisEntitlementsCache : IDistributedEntitlementsCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisEntitlementsCache> _logger;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(15);

    public RedisEntitlementsCache(IDistributedCache cache, ILogger<RedisEntitlementsCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<EntitlementsResponse?> GetAsync(string userId)
    {
        try
        {
            var key = GetCacheKey(userId);
            var cached = await _cache.GetStringAsync(key);
            if (cached == null) return null;

            return JsonSerializer.Deserialize<EntitlementsResponse>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get entitlements from cache for user {UserId}", userId);
            return null;
        }
    }

    public async Task SetAsync(string userId, EntitlementsResponse entitlements, TimeSpan? expiry = null)
    {
        try
        {
            var key = GetCacheKey(userId);
            var json = JsonSerializer.Serialize(entitlements);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
            };
            await _cache.SetStringAsync(key, json, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache entitlements for user {UserId}", userId);
        }
    }

    public async Task RemoveAsync(string userId)
    {
        try
        {
            var key = GetCacheKey(userId);
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove entitlements cache for user {UserId}", userId);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        // Redis-specific implementation would use SCAN + DEL
        // For IDistributedCache, we'll implement a basic approach
        _logger.LogInformation("Cache invalidation by pattern requested: {Pattern}", pattern);
        // In production, implement Redis SCAN/DEL pattern matching
        await Task.CompletedTask;
    }

    private static string GetCacheKey(string userId) => $"entitlements:user:{userId}";
}
