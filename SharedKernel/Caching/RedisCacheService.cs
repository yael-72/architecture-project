using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SharedKernel.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly string _instanceName;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RedisCacheService(
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _redis = redis;
        _logger = logger;
        _instanceName = configuration.GetValue<string>("Cache:InstanceName", "StoreApi:")!;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var data = await _cache.GetStringAsync(key);
        if (data == null) return default;
        _logger.LogDebug("Cache HIT for key: {CacheKey}", key);
        return JsonSerializer.Deserialize<T>(data, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        var data = JsonSerializer.Serialize(value, JsonOptions);
        await _cache.SetStringAsync(key, data, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        });
        _logger.LogDebug("Cache SET for key: {CacheKey}, TTL: {TTL}s", key, expiration.TotalSeconds);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
        _logger.LogDebug("Cache REMOVE for key: {CacheKey}", key);
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        var db = _redis.GetDatabase();
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{_instanceName}{prefix}*").ToArray();
        if (keys.Length == 0) return;
        await db.KeyDeleteAsync(keys);
        _logger.LogDebug("Cache INVALIDATE: removed {Count} keys with prefix '{Prefix}'", keys.Length, prefix);
    }
}
