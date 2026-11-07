using InventoryService.Data;
using InventoryService.Interfaces;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Repositories;

public class InventoryRepository : IInventoryRepository
{
    private readonly InventoryDbContext _db;

    public InventoryRepository(InventoryDbContext db) => _db = db;

    public Task<InventoryItem?> GetByProductIdAsync(int productId) =>
        _db.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId);

    public async Task<IEnumerable<InventoryItem>> GetAllAsync() =>
        await _db.InventoryItems.OrderBy(i => i.ProductId).ToListAsync();

    public async Task<InventoryItem> UpsertAsync(int productId, int stockQuantity)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item == null)
        {
            item = new InventoryItem
            {
                ProductId = productId,
                StockQuantity = stockQuantity,
                ReservedQuantity = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.InventoryItems.Add(item);
        }
        else
        {
            item.StockQuantity = stockQuantity;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeductStockAsync(int productId, int quantity)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item == null || item.AvailableQuantity < quantity)
            return false;

        item.StockQuantity -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RestoreStockAsync(int productId, int quantity)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.ProductId == productId);
        if (item == null)
            return false;

        item.StockQuantity += quantity;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
