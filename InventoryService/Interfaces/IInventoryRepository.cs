using InventoryService.Models;

namespace InventoryService.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryItem?> GetByProductIdAsync(int productId);
    Task<IEnumerable<InventoryItem>> GetAllAsync();
    Task<InventoryItem> UpsertAsync(int productId, int stockQuantity);
    Task<bool> DeductStockAsync(int productId, int quantity);
    Task<bool> RestoreStockAsync(int productId, int quantity);
}
