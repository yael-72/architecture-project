using System.Net.Http.Json;
using BffService.DTOs;

namespace BffService.Clients;

public class UserClient : IUserClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserClient> _logger;

    public UserClient(HttpClient httpClient, ILogger<UserClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<DownstreamUserDto?> GetUserByIdAsync(int id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<DownstreamUserDto>($"api/users/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user {UserId} from UserAuthService", id);
            return null;
        }
    }
}
