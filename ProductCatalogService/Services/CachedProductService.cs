using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;
using SharedKernel.Caching;
using SharedKernel.DTOs;

namespace ProductCatalogService.Services;

public class CachedProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly ICacheService _cache;
    private readonly ILogger<CachedProductService> _logger;
    private readonly TimeSpan _ttl;

    private const string AllKey = "catalog:products:all";
    private const string PagedPrefix = "catalog:products:paged:";
    private const string IdPrefix = "catalog:products:id:";
    private const string CategoryPrefix = "catalog:products:category:";
    private const string SearchPrefix = "catalog:products:search:";
    private const string ProductsPrefix = "catalog:products:";

    public CachedProductService(
        IProductService inner,
        ICacheService cache,
        ILogger<CachedProductService> logger,
        IConfiguration configuration)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(configuration.GetValue<int>("Cache:ProductTtlSeconds", 60));
    }

    public async Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync()
    {
        var cached = await _cache.GetAsync<IEnumerable<ProductResponseDto>>(AllKey);
        if (cached != null) return cached;
        var result = await _inner.GetAllProductsAsync();
        await _cache.SetAsync(AllKey, result, _ttl);
        return result;
    }

    public async Task<PagedResult<ProductResponseDto>> GetAllProductsPagedAsync(PaginationParams paginationParams)
    {
        var key = $"{PagedPrefix}{paginationParams.PageNumber}:{paginationParams.PageSize}";
        var cached = await _cache.GetAsync<PagedResult<ProductResponseDto>>(key);
        if (cached != null) return cached;
        var result = await _inner.GetAllProductsPagedAsync(paginationParams);
        await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<ProductResponseDto?> GetProductByIdAsync(int id)
    {
        var key = $"{IdPrefix}{id}";
        var cached = await _cache.GetAsync<ProductResponseDto>(key);
        if (cached != null) return cached;
        var result = await _inner.GetProductByIdAsync(id);
        if (result != null) await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetProductsByCategoryAsync(int categoryId)
    {
        var key = $"{CategoryPrefix}{categoryId}";
        var cached = await _cache.GetAsync<IEnumerable<ProductResponseDto>>(key);
        if (cached != null) return cached;
        var result = await _inner.GetProductsByCategoryAsync(categoryId);
        await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<IEnumerable<ProductResponseDto>> SearchProductsByNameAsync(string searchTerm)
    {
        var key = $"{SearchPrefix}{searchTerm.ToLowerInvariant()}";
        var cached = await _cache.GetAsync<IEnumerable<ProductResponseDto>>(key);
        if (cached != null) return cached;
        var result = await _inner.SearchProductsByNameAsync(searchTerm);
        await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<PagedResult<ProductResponseDto>> SearchProductsByNamePagedAsync(string searchTerm, PaginationParams paginationParams)
    {
        var key = $"{SearchPrefix}paged:{searchTerm.ToLowerInvariant()}:{paginationParams.PageNumber}:{paginationParams.PageSize}";
        var cached = await _cache.GetAsync<PagedResult<ProductResponseDto>>(key);
        if (cached != null) return cached;
        var result = await _inner.SearchProductsByNamePagedAsync(searchTerm, paginationParams);
        await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<ProductResponseDto> CreateProductAsync(ProductCreateDto createDto)
    {
        var result = await _inner.CreateProductAsync(createDto);
        await _cache.RemoveByPrefixAsync(ProductsPrefix);
        return result;
    }

    public async Task<ProductResponseDto?> UpdateProductAsync(int id, ProductUpdateDto updateDto)
    {
        var result = await _inner.UpdateProductAsync(id, updateDto);
        if (result != null) await _cache.RemoveByPrefixAsync(ProductsPrefix);
        return result;
    }

    public async Task<bool> DeleteProductAsync(int id)
    {
        var result = await _inner.DeleteProductAsync(id);
        if (result) await _cache.RemoveByPrefixAsync(ProductsPrefix);
        return result;
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var result = await _inner.DeductStockAsync(productId, quantity);
        if (result) await _cache.RemoveByPrefixAsync(ProductsPrefix);
        return result;
    }
}
