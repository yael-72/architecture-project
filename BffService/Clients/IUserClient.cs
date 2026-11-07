using BffService.DTOs;

namespace BffService.Clients;

public interface IUserClient
{
    Task<DownstreamUserDto?> GetUserByIdAsync(int id);
}
