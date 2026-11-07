using OrderService.DTOs;

namespace OrderService.Interfaces;

public interface IOrderService
{
    Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync();
    Task<OrderResponseDto?> GetOrderByIdAsync(int id);
    Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId);
    Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto createDto);
    Task<OrderResponseDto?> UpdateOrderAsync(int id, OrderUpdateDto updateDto);
    Task<bool> DeleteOrderAsync(int id);
}
