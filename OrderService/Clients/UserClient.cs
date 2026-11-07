using System.Net.Http.Json;
using OrderService.DTOs;
using OrderService.Interfaces;

namespace OrderService.Clients;

public class UserClient : IUserClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserClient> _logger;

    public UserClient(HttpClient httpClient, ILogger<UserClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/users/{userId}");
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("UserAuthService returned {Status} for user {UserId}", response.StatusCode, userId);
                return null;
            }
            return await response.Content.ReadFromJsonAsync<UserDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user {UserId} from UserAuthService", userId);
            return null;
        }
    }

    public async Task<bool> UserExistsAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        return user != null;
    }
}
