using InventoryService.DTOs;
using InventoryService.Interfaces;
using InventoryService.Models;

namespace InventoryService.Services;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repo;

    public InventoryService(IInventoryRepository repo) => _repo = repo;

    public async Task<IEnumerable<InventoryItemResponseDto>> GetAllAsync()
    {
        var items = await _repo.GetAllAsync();
        return items.Select(MapToDto);
    }

    public async Task<InventoryItemResponseDto?> GetByProductIdAsync(int productId)
    {
        var item = await _repo.GetByProductIdAsync(productId);
        return item != null ? MapToDto(item) : null;
    }

    public async Task<InventoryItemResponseDto> UpsertAsync(InventoryUpsertDto dto)
    {
        var item = await _repo.UpsertAsync(dto.ProductId, dto.StockQuantity);
        return MapToDto(item);
    }

    public Task<bool> DeductStockAsync(int productId, int quantity) =>
        _repo.DeductStockAsync(productId, quantity);

    public Task<bool> RestoreStockAsync(int productId, int quantity) =>
        _repo.RestoreStockAsync(productId, quantity);

    private static InventoryItemResponseDto MapToDto(InventoryItem item) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        StockQuantity = item.StockQuantity,
        ReservedQuantity = item.ReservedQuantity,
        AvailableQuantity = item.AvailableQuantity,
        UpdatedAt = item.UpdatedAt
    };
}
