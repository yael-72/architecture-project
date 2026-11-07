using System.Net.Http.Json;
using BffService.DTOs;

namespace BffService.Clients;

public class OrderClient : IOrderClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderClient> _logger;

    public OrderClient(HttpClient httpClient, ILogger<OrderClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<DownstreamOrderDto>?> GetOrdersByUserIdAsync(int userId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<DownstreamOrderDto>>(
                $"internal/internalorders/user/{userId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch orders for user {UserId} from OrderService", userId);
            return null;
        }
    }
}
