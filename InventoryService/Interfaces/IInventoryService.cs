using InventoryService.DTOs;

namespace InventoryService.Interfaces;

public interface IInventoryService
{
    Task<IEnumerable<InventoryItemResponseDto>> GetAllAsync();
    Task<InventoryItemResponseDto?> GetByProductIdAsync(int productId);
    Task<InventoryItemResponseDto> UpsertAsync(InventoryUpsertDto dto);
    Task<bool> DeductStockAsync(int productId, int quantity);
    Task<bool> RestoreStockAsync(int productId, int quantity);
}
