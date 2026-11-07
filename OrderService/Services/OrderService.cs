using OrderService.DTOs;
using OrderService.Interfaces;
using OrderService.Models;
using SharedKernel.Events;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICatalogClient _catalogClient;
    private readonly IUserClient _userClient;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        ICatalogClient catalogClient,
        IUserClient userClient,
        IMessagePublisher publisher,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _catalogClient = catalogClient;
        _userClient = userClient;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync()
    {
        var orders = await _orderRepository.GetAllAsync();
        return orders.Select(MapToResponseDto);
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(int id)
    {
        var order = await _orderRepository.GetByIdAsync(id);
        return order != null ? MapToResponseDto(order) : null;
    }

    public async Task<IEnumerable<OrderResponseDto>> GetOrdersByUserIdAsync(int userId)
    {
        var orders = await _orderRepository.GetByUserIdAsync(userId);
        return orders.Select(MapToResponseDto);
    }

    public async Task<OrderResponseDto> CreateOrderAsync(OrderCreateDto createDto)
    {
        // Validate user exists via HTTP call to UserAuthService
        var user = await _userClient.GetUserByIdAsync(createDto.UserId);
        if (user == null)
            throw new ArgumentException($"User with ID {createDto.UserId} does not exist.");

        var orderItems = new List<OrderItem>();
        decimal totalAmount = 0;

        foreach (var itemDto in createDto.OrderItems)
        {
            // Validate product exists and get its price from ProductCatalogService
            // NOTE: stock checking and deduction is now handled by InventoryService via the saga
            var product = await _catalogClient.GetProductByIdAsync(itemDto.ProductId);
            if (product == null)
                throw new ArgumentException($"Product with ID {itemDto.ProductId} does not exist.");

            var subtotal = product.Price * itemDto.Quantity;
            totalAmount += subtotal;

            orderItems.Add(new OrderItem
            {
                ProductId = itemDto.ProductId,
                Quantity = itemDto.Quantity,
                UnitPrice = product.Price,
                Subtotal = subtotal
            });
        }

        // Create order as Pending — InventoryService will confirm or reject it asynchronously
        var order = new Order
        {
            UserId = createDto.UserId,
            ShippingAddress = createDto.ShippingAddress,
            TotalAmount = totalAmount,
            Status = "Pending",
            OrderItems = orderItems
        };

        var created = await _orderRepository.CreateAsync(order);
        _logger.LogInformation(
            "Order {OrderId} created as Pending [CorrelationId: {CorrelationId}]",
            created.Id, CorrelationContext.CorrelationId);

        // Saga Step 1 — publish OrderPlaced; InventoryService picks this up
        await _publisher.PublishAsync(QueueNames.OrderPlaced, new OrderPlacedEvent
        {
            CorrelationId = CorrelationContext.CorrelationId,
            OrderId = created.Id,
            UserId = created.UserId,
            Items = orderItems.Select(i => new OrderItemLine
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity
            }).ToList()
        });

        var orderWithDetails = await _orderRepository.GetByIdAsync(created.Id);
        var dto = MapToResponseDto(orderWithDetails!);
        dto.UserName = $"{user.FirstName} {user.LastName}";
        return dto;
    }

    public async Task<OrderResponseDto?> UpdateOrderAsync(int id, OrderUpdateDto updateDto)
    {
        var existing = await _orderRepository.GetByIdAsync(id);
        if (existing == null) return null;

        if (updateDto.ShippingAddress != null)
            existing.ShippingAddress = updateDto.ShippingAddress;

        if (updateDto.Status != null)
        {
            var validStatuses = new[] { "Pending", "Confirmed", "Processing", "Shipped", "Delivered", "Cancelled" };
            if (!validStatuses.Contains(updateDto.Status))
                throw new ArgumentException($"Invalid status. Valid values: {string.Join(", ", validStatuses)}");

            existing.Status = updateDto.Status;
            if (updateDto.Status == "Shipped" && !existing.ShippedDate.HasValue)
                existing.ShippedDate = DateTime.UtcNow;
            if (updateDto.Status == "Delivered" && !existing.DeliveredDate.HasValue)
                existing.DeliveredDate = DateTime.UtcNow;
        }

        var updated = await _orderRepository.UpdateAsync(existing);
        return updated != null ? MapToResponseDto(updated) : null;
    }

    public async Task<bool> DeleteOrderAsync(int id) =>
        await _orderRepository.DeleteAsync(id);

    private static OrderResponseDto MapToResponseDto(Order order) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        TotalAmount = order.TotalAmount,
        Status = order.Status,
        ShippingAddress = order.ShippingAddress,
        OrderDate = order.OrderDate,
        ShippedDate = order.ShippedDate,
        DeliveredDate = order.DeliveredDate,
        OrderItems = order.OrderItems?.Select(oi => new OrderItemResponseDto
        {
            Id = oi.Id,
            ProductId = oi.ProductId,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice,
            Subtotal = oi.Subtotal
        }).ToList() ?? new List<OrderItemResponseDto>()
    };
}
