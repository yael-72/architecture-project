using Microsoft.Extensions.Logging;
using Moq;
using OrderService.DTOs;
using OrderService.Interfaces;
using OrderService.Models;
using SharedKernel.Messaging;

namespace OrderService.Tests;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepoMock = new();
    private readonly Mock<ICatalogClient> _catalogClientMock = new();
    private readonly Mock<IUserClient> _userClientMock = new();
    private readonly Mock<IMessagePublisher> _publisherMock = new();
    private readonly Mock<ILogger<Services.OrderService>> _loggerMock = new();

    private Services.OrderService CreateSut() => new(
        _orderRepoMock.Object,
        _catalogClientMock.Object,
        _userClientMock.Object,
        _publisherMock.Object,
        _loggerMock.Object);

    // ── CreateOrderAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_WhenUserNotFound_ThrowsArgumentException()
    {
        // Arrange
        _userClientMock.Setup(x => x.GetUserByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((UserDto?)null);

        var sut = CreateSut();
        var dto = new OrderCreateDto
        {
            UserId = 99,
            ShippingAddress = "123 Main St",
            OrderItems = new List<OrderItemCreateDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateOrderAsync(dto));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task CreateOrder_WhenProductNotFound_ThrowsArgumentException()
    {
        // Arrange
        _userClientMock.Setup(x => x.GetUserByIdAsync(1))
            .ReturnsAsync(new UserDto { Id = 1, FirstName = "Jane", LastName = "Doe" });

        _catalogClientMock.Setup(x => x.GetProductByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((ProductDto?)null);

        var sut = CreateSut();
        var dto = new OrderCreateDto
        {
            UserId = 1,
            ShippingAddress = "123 Main St",
            OrderItems = new List<OrderItemCreateDto> { new() { ProductId = 42, Quantity = 1 } }
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateOrderAsync(dto));
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public async Task CreateOrder_HappyPath_ReturnsPendingOrderAndPublishesEvent()
    {
        // Arrange
        var user = new UserDto { Id = 1, FirstName = "Jane", LastName = "Doe" };
        var product = new ProductDto { Id = 1, Name = "Laptop", Price = 2500m, Stock = 10 };

        _userClientMock.Setup(x => x.GetUserByIdAsync(1)).ReturnsAsync(user);
        _catalogClientMock.Setup(x => x.GetProductByIdAsync(1)).ReturnsAsync(product);

        var savedOrder = new Order
        {
            Id = 1,
            UserId = 1,
            Status = "Pending",
            TotalAmount = 2500m,
            ShippingAddress = "123 Main St",
            OrderDate = DateTime.UtcNow,
            OrderItems = new List<OrderItem>
            {
                new() { Id = 1, ProductId = 1, Quantity = 1, UnitPrice = 2500m, Subtotal = 2500m }
            }
        };

        _orderRepoMock.Setup(x => x.CreateAsync(It.IsAny<Order>())).ReturnsAsync(savedOrder);
        _orderRepoMock.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(savedOrder);

        var sut = CreateSut();
        var dto = new OrderCreateDto
        {
            UserId = 1,
            ShippingAddress = "123 Main St",
            OrderItems = new List<OrderItemCreateDto> { new() { ProductId = 1, Quantity = 1 } }
        };

        // Act
        var result = await sut.CreateOrderAsync(dto);

        // Assert
        Assert.Equal("Pending", result.Status);
        Assert.Equal(2500m, result.TotalAmount);
        Assert.Equal("Jane Doe", result.UserName);

        // Verify saga event was published
        _publisherMock.Verify(
            x => x.PublishAsync("order.placed", It.IsAny<object>()),
            Times.Once);
    }

    // ── GetOrderByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderById_WhenExists_ReturnsMappedDto()
    {
        // Arrange
        var order = new Order
        {
            Id = 5,
            UserId = 1,
            Status = "Confirmed",
            TotalAmount = 100m,
            ShippingAddress = "Test St",
            OrderDate = DateTime.UtcNow,
            OrderItems = new List<OrderItem>()
        };
        _orderRepoMock.Setup(x => x.GetByIdAsync(5)).ReturnsAsync(order);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOrderByIdAsync(5);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
        Assert.Equal("Confirmed", result.Status);
    }

    [Fact]
    public async Task GetOrderById_WhenNotFound_ReturnsNull()
    {
        // Arrange
        _orderRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((Order?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetOrderByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    // ── GetOrdersByUserId ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrdersByUserId_ReturnsOnlyUserOrders()
    {
        // Arrange
        var orders = new List<Order>
        {
            new() { Id = 1, UserId = 2, Status = "Confirmed", TotalAmount = 50m,
                    ShippingAddress = "A", OrderDate = DateTime.UtcNow, OrderItems = new List<OrderItem>() },
            new() { Id = 2, UserId = 2, Status = "Pending",   TotalAmount = 75m,
                    ShippingAddress = "B", OrderDate = DateTime.UtcNow, OrderItems = new List<OrderItem>() }
        };
        _orderRepoMock.Setup(x => x.GetByUserIdAsync(2)).ReturnsAsync(orders);

        var sut = CreateSut();

        // Act
        var result = (await sut.GetOrdersByUserIdAsync(2)).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, o => Assert.Equal(2, o.UserId));
    }
}
