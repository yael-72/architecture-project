using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using OrderService.DTOs;
using OrderService.Interfaces;

namespace OrderService.Clients;

// Caching wrapper so repeated product lookups during order creation don't hit CatalogService multiple times.
public class CachedCatalogClient : ICatalogClient
{
    private readonly ICatalogClient _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedCatalogClient> _logger;
    private static readonly TimeSpan ProductCacheTtl = TimeSpan.FromSeconds(30);

    public CachedCatalogClient(ICatalogClient inner, IDistributedCache cache, ILogger<CachedCatalogClient> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductByIdAsync(int productId)
    {
        var cacheKey = $"order:catalog:product:{productId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            _logger.LogDebug("Cache HIT for product {ProductId}", productId);
            return JsonSerializer.Deserialize<ProductDto>(cached);
        }

        var product = await _inner.GetProductByIdAsync(productId);
        if (product != null)
        {
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ProductCacheTtl });
        }
        return product;
    }

    // Stock deduction must always hit the real service — skip cache for mutations
    public Task<bool> DeductStockAsync(int productId, int quantity) =>
        _inner.DeductStockAsync(productId, quantity);
}
