using OrderService.DTOs;

namespace OrderService.Interfaces;

// Typed HTTP client to talk to UserAuthService
public interface IUserClient
{
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<bool> UserExistsAsync(int userId);
}
