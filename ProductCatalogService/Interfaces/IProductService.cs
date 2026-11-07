using ProductCatalogService.DTOs;
using SharedKernel.DTOs;

namespace ProductCatalogService.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductResponseDto>> GetAllProductsAsync();
    Task<PagedResult<ProductResponseDto>> GetAllProductsPagedAsync(PaginationParams paginationParams);
    Task<ProductResponseDto?> GetProductByIdAsync(int id);
    Task<IEnumerable<ProductResponseDto>> GetProductsByCategoryAsync(int categoryId);
    Task<IEnumerable<ProductResponseDto>> SearchProductsByNameAsync(string searchTerm);
    Task<PagedResult<ProductResponseDto>> SearchProductsByNamePagedAsync(string searchTerm, PaginationParams paginationParams);
    Task<ProductResponseDto> CreateProductAsync(ProductCreateDto createDto);
    Task<ProductResponseDto?> UpdateProductAsync(int id, ProductUpdateDto updateDto);
    Task<bool> DeleteProductAsync(int id);
    Task<bool> DeductStockAsync(int productId, int quantity);
}
