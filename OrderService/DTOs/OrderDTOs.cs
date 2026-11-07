using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class OrderCreateDto
{
    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<OrderItemCreateDto> OrderItems { get; set; } = new();
}

public class OrderItemCreateDto
{
    [Required]
    public int ProductId { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class OrderUpdateDto
{
    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    public string? Status { get; set; }
}

public class OrderResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ShippingAddress { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public List<OrderItemResponseDto> OrderItems { get; set; } = new();
}

public class OrderItemResponseDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

// DTOs received from ProductCatalogService via HTTP
public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class StockDeductDto
{
    public int ProductId { get; set; }
    public int QuantityChange { get; set; }
}

// DTOs received from UserAuthService via HTTP
public class UserDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
