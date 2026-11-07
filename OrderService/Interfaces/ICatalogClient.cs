using OrderService.DTOs;

namespace OrderService.Interfaces;

// Typed HTTP client to talk to ProductCatalogService
public interface ICatalogClient
{
    Task<ProductDto?> GetProductByIdAsync(int productId);
    Task<bool> DeductStockAsync(int productId, int quantity);
}
