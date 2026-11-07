namespace InventoryService.Models;

public class InventoryItem
{
    public int Id { get; set; }

    /// <summary>Matches the ProductId from ProductCatalogService (no FK — separate DB).</summary>
    public int ProductId { get; set; }

    /// <summary>Total physical units in warehouse.</summary>
    public int StockQuantity { get; set; }

    /// <summary>Units currently held for pending/processing orders.</summary>
    public int ReservedQuantity { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>Units that can still be sold (computed, not persisted).</summary>
    public int AvailableQuantity => StockQuantity - ReservedQuantity;
}
