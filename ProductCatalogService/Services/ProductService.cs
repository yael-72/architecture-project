using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;
using ProductCatalogService.Models;
using SharedKernel.DTOs;

namespace ProductCatalogService.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync()
    {
        var products = await _productRepository.GetAllAsync();
        return products.Select(MapToResponseDto);
    }

    public async Task<PagedResult<ProductResponseDto>> GetAllProductsPagedAsync(PaginationParams paginationParams)
    {
        var (items, totalCount) = await _productRepository.GetAllPagedAsync(paginationParams.PageNumber, paginationParams.PageSize);
        return new PagedResult<ProductResponseDto>
        {
            Items = items.Select(MapToResponseDto),
            PageNumber = paginationParams.PageNumber,
            PageSize = paginationParams.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)paginationParams.PageSize)
        };
    }

    public async Task<ProductResponseDto?> GetProductByIdAsync(int id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        return product != null ? MapToResponseDto(product) : null;
    }

    public async Task<IEnumerable<ProductResponseDto>> GetProductsByCategoryAsync(int categoryId)
    {
        var products = await _productRepository.GetByCategoryAsync(categoryId);
        return products.Select(MapToResponseDto);
    }

    public async Task<IEnumerable<ProductResponseDto>> SearchProductsByNameAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return Enumerable.Empty<ProductResponseDto>();
        var products = await _productRepository.SearchByNameAsync(searchTerm);
        return products.Select(MapToResponseDto);
    }

    public async Task<PagedResult<ProductResponseDto>> SearchProductsByNamePagedAsync(string searchTerm, PaginationParams paginationParams)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new PagedResult<ProductResponseDto> { Items = Enumerable.Empty<ProductResponseDto>(), PageNumber = paginationParams.PageNumber, PageSize = paginationParams.PageSize };

        var (items, totalCount) = await _productRepository.SearchByNamePagedAsync(searchTerm, paginationParams.PageNumber, paginationParams.PageSize);
        return new PagedResult<ProductResponseDto>
        {
            Items = items.Select(MapToResponseDto),
            PageNumber = paginationParams.PageNumber,
            PageSize = paginationParams.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)paginationParams.PageSize)
        };
    }

    public async Task<ProductResponseDto> CreateProductAsync(ProductCreateDto createDto)
    {
        if (!await _categoryRepository.ExistsAsync(createDto.CategoryId))
            throw new ArgumentException($"Category with ID {createDto.CategoryId} does not exist.");

        var product = new Product
        {
            Name = createDto.Name,
            Description = createDto.Description,
            Price = createDto.Price,
            Stock = createDto.Stock,
            CategoryId = createDto.CategoryId
        };

        var created = await _productRepository.CreateAsync(product);
        _logger.LogInformation("Product created with ID: {ProductId}", created.Id);
        return MapToResponseDto(created);
    }

    public async Task<ProductResponseDto?> UpdateProductAsync(int id, ProductUpdateDto updateDto)
    {
        var existing = await _productRepository.GetByIdAsync(id);
        if (existing == null) return null;

        if (updateDto.Name != null) existing.Name = updateDto.Name;
        if (updateDto.Description != null) existing.Description = updateDto.Description;
        if (updateDto.Price.HasValue) existing.Price = updateDto.Price.Value;
        if (updateDto.Stock.HasValue) existing.Stock = updateDto.Stock.Value;
        if (updateDto.CategoryId.HasValue)
        {
            if (!await _categoryRepository.ExistsAsync(updateDto.CategoryId.Value))
                throw new ArgumentException($"Category with ID {updateDto.CategoryId} does not exist.");
            existing.CategoryId = updateDto.CategoryId.Value;
        }

        var updated = await _productRepository.UpdateAsync(existing);
        return updated != null ? MapToResponseDto(updated) : null;
    }

    public async Task<bool> DeleteProductAsync(int id) =>
        await _productRepository.DeleteAsync(id);

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null || product.Stock < quantity) return false;

        product.Stock -= quantity;
        var updated = await _productRepository.UpdateAsync(product);
        return updated != null;
    }

    private static ProductResponseDto MapToResponseDto(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Stock = product.Stock,
        CategoryId = product.CategoryId,
        CategoryName = product.Category?.Name ?? string.Empty,
        CreatedAt = product.CreatedAt
    };
}
