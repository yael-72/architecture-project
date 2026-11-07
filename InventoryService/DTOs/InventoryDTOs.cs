using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs;

public class InventoryItemResponseDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int StockQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class InventoryUpsertDto
{
    [Required]
    public int ProductId { get; set; }

    [Required, Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }
}
