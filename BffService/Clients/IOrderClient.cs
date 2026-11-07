using BffService.DTOs;

namespace BffService.Clients;

public interface IOrderClient
{
    Task<IEnumerable<DownstreamOrderDto>?> GetOrdersByUserIdAsync(int userId);
}
