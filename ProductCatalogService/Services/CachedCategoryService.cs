using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;
using SharedKernel.Caching;

namespace ProductCatalogService.Services;

public class CachedCategoryService : ICategoryService
{
    private readonly ICategoryService _inner;
    private readonly ICacheService _cache;
    private readonly ILogger<CachedCategoryService> _logger;
    private readonly TimeSpan _ttl;

    private const string AllKey = "catalog:categories:all";
    private const string IdPrefix = "catalog:categories:id:";
    private const string CategoriesPrefix = "catalog:categories:";
    private const string ProductsPrefix = "catalog:products:";

    public CachedCategoryService(
        ICategoryService inner,
        ICacheService cache,
        ILogger<CachedCategoryService> logger,
        IConfiguration configuration)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(configuration.GetValue<int>("Cache:CategoryTtlSeconds", 300));
    }

    public async Task<IEnumerable<CategoryResponseDto>> GetAllCategoriesAsync()
    {
        var cached = await _cache.GetAsync<IEnumerable<CategoryResponseDto>>(AllKey);
        if (cached != null) return cached;
        var result = await _inner.GetAllCategoriesAsync();
        await _cache.SetAsync(AllKey, result, _ttl);
        return result;
    }

    public async Task<CategoryResponseDto?> GetCategoryByIdAsync(int id)
    {
        var key = $"{IdPrefix}{id}";
        var cached = await _cache.GetAsync<CategoryResponseDto>(key);
        if (cached != null) return cached;
        var result = await _inner.GetCategoryByIdAsync(id);
        if (result != null) await _cache.SetAsync(key, result, _ttl);
        return result;
    }

    public async Task<CategoryResponseDto> CreateCategoryAsync(CategoryCreateDto createDto)
    {
        var result = await _inner.CreateCategoryAsync(createDto);
        await InvalidateCachesAsync();
        return result;
    }

    public async Task<CategoryResponseDto?> UpdateCategoryAsync(int id, CategoryUpdateDto updateDto)
    {
        var result = await _inner.UpdateCategoryAsync(id, updateDto);
        if (result != null) await InvalidateCachesAsync();
        return result;
    }

    public async Task<bool> DeleteCategoryAsync(int id)
    {
        var result = await _inner.DeleteCategoryAsync(id);
        if (result) await InvalidateCachesAsync();
        return result;
    }

    private async Task InvalidateCachesAsync()
    {
        await _cache.RemoveByPrefixAsync(CategoriesPrefix);
        await _cache.RemoveByPrefixAsync(ProductsPrefix);
        _logger.LogDebug("Category mutation: invalidated categories and products caches");
    }
}
