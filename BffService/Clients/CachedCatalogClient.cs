using System.Text;
using System.Text.Json;
using BffService.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using SharedKernel.DTOs;

namespace BffService.Clients;

public class CachedCatalogClient : ICatalogClient
{
    private readonly CatalogClient _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedCatalogClient> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public CachedCatalogClient(CatalogClient inner, IDistributedCache cache, ILogger<CachedCatalogClient> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<PagedResult<DownstreamProductDto>?> GetProductsAsync(int pageNumber, int pageSize)
    {
        var key = $"bff:catalog:products:page:{pageNumber}:size:{pageSize}";
        var cached = await _cache.GetAsync(key);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return JsonSerializer.Deserialize<PagedResult<DownstreamProductDto>>(Encoding.UTF8.GetString(cached));
        }

        var result = await _inner.GetProductsAsync(pageNumber, pageSize);
        if (result != null)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        }

        return result;
    }

    public async Task<DownstreamProductDto?> GetProductByIdAsync(int id)
    {
        var key = $"bff:catalog:product:{id}";
        var cached = await _cache.GetAsync(key);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return JsonSerializer.Deserialize<DownstreamProductDto>(Encoding.UTF8.GetString(cached));
        }

        var result = await _inner.GetProductByIdAsync(id);
        if (result != null)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        }

        return result;
    }

    public async Task<IEnumerable<DownstreamCategoryDto>?> GetCategoriesAsync()
    {
        var key = "bff:catalog:categories";
        var cached = await _cache.GetAsync(key);
        if (cached != null)
        {
            _logger.LogDebug("Cache hit for {Key}", key);
            return JsonSerializer.Deserialize<IEnumerable<DownstreamCategoryDto>>(Encoding.UTF8.GetString(cached));
        }

        var result = await _inner.GetCategoriesAsync();
        if (result != null)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));
            await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
        }

        return result;
    }
}
