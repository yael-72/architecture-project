using OrderService.Models;

namespace OrderService.Interfaces;

public interface IOrderRepository
{
    Task<IEnumerable<Order>> GetAllAsync();
    Task<Order?> GetByIdAsync(int id);
    Task<IEnumerable<Order>> GetByUserIdAsync(int userId);
    Task<Order> CreateAsync(Order order);
    Task<Order?> UpdateAsync(Order order);
    Task<bool> DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
}
