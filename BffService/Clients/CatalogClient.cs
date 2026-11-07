using System.Net.Http.Json;
using BffService.DTOs;
using SharedKernel.DTOs;

namespace BffService.Clients;

public class CatalogClient : ICatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogClient> _logger;

    public CatalogClient(HttpClient httpClient, ILogger<CatalogClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PagedResult<DownstreamProductDto>?> GetProductsAsync(int pageNumber, int pageSize)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PagedResult<DownstreamProductDto>>(
                $"api/products/paged?pageNumber={pageNumber}&pageSize={pageSize}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch products from CatalogService");
            return null;
        }
    }

    public async Task<DownstreamProductDto?> GetProductByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DownstreamProductDto>($"api/products/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch product {ProductId} from CatalogService", id);
            return null;
        }
    }

    public async Task<IEnumerable<DownstreamCategoryDto>?> GetCategoriesAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<DownstreamCategoryDto>>("api/categories");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch categories from CatalogService");
            return null;
        }
    }
}
