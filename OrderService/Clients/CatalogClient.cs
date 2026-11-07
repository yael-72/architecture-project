using System.Net.Http.Json;
using OrderService.DTOs;
using OrderService.Interfaces;

namespace OrderService.Clients;

public class CatalogClient : ICatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogClient> _logger;

    public CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProductDto?> GetProductByIdAsync(int productId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"internal/internalproducts/{productId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CatalogService returned {Status} for product {ProductId}", response.StatusCode, productId);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<ProductDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch product {ProductId} from CatalogService", productId);
            return null;
        }
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        try
        {
            var payload = new StockDeductDto { ProductId = productId, QuantityChange = quantity };
            var response = await _httpClient.PostAsJsonAsync("internal/internalproducts/deduct-stock", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deduct stock for product {ProductId}", productId);
            return false;
        }
    }
}
