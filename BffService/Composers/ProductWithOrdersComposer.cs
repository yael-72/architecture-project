using BffService.Clients;
using BffService.DTOs;

namespace BffService.Composers;

public class ProductWithOrdersComposer
{
    private readonly ICatalogClient _catalogClient;
    private readonly IOrderClient _orderClient;
    private readonly IUserClient _userClient;
    private readonly ILogger<ProductWithOrdersComposer> _logger;

    public ProductWithOrdersComposer(
        ICatalogClient catalogClient,
        IOrderClient orderClient,
        IUserClient userClient,
        ILogger<ProductWithOrdersComposer> logger)
    {
        _catalogClient = catalogClient;
        _orderClient = orderClient;
        _userClient = userClient;
        _logger = logger;
    }

    public async Task<UserDashboardDto?> ComposeUserDashboardAsync(int userId)
    {
        var userTask = _userClient.GetUserByIdAsync(userId);
        var ordersTask = _orderClient.GetOrdersByUserIdAsync(userId);

        await Task.WhenAll(userTask, ordersTask);

        var user = await userTask;
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found when composing dashboard", userId);
            return null;
        }

        var orders = await ordersTask ?? Enumerable.Empty<DownstreamOrderDto>();
        var orderList = orders.ToList();

        return new UserDashboardDto
        {
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}",
            Email = user.Email,
            TotalOrders = orderList.Count,
            TotalSpent = orderList.Sum(o => o.TotalAmount),
            RecentOrders = orderList
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    TotalAmount = o.TotalAmount,
                    Status = o.Status,
                    OrderDate = o.OrderDate,
                    ItemCount = o.OrderItems.Sum(i => i.Quantity)
                })
                .ToList()
        };
    }

    public async Task<CatalogPageDto> ComposeCatalogPageAsync(int pageNumber, int pageSize)
    {
        var productsTask = _catalogClient.GetProductsAsync(pageNumber, pageSize);
        var categoriesTask = _catalogClient.GetCategoriesAsync();

        await Task.WhenAll(productsTask, categoriesTask);

        var pagedProducts = await productsTask;
        var categories = (await categoriesTask)?.ToList() ?? new List<DownstreamCategoryDto>();

        var products = pagedProducts?.Items?.Select(p => new ProductSummaryDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Stock = p.Stock,
            CategoryName = p.CategoryName
        }).ToList() ?? new List<ProductSummaryDto>();

        return new CatalogPageDto
        {
            Products = products,
            TotalProducts = pagedProducts?.TotalCount ?? 0,
            Categories = categories.Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                ProductCount = c.ProductCount
            }).ToList()
        };
    }
}
