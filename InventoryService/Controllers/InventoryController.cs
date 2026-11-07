using InventoryService.DTOs;
using InventoryService.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _service;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService service, ILogger<InventoryController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get stock levels for all products.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.GetAllAsync());

    /// <summary>Get stock level for a specific product.</summary>
    [HttpGet("{productId:int}")]
    public async Task<IActionResult> GetByProductId(int productId)
    {
        var item = await _service.GetByProductIdAsync(productId);
        return item == null ? NotFound(new { message = $"No inventory record for product {productId}" }) : Ok(item);
    }

    /// <summary>Create or update stock level for a product (Admin/Manager only).</summary>
    [HttpPut("{productId:int}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Upsert(int productId, [FromBody] InventoryUpsertDto dto)
    {
        dto.ProductId = productId;
        var result = await _service.UpsertAsync(dto);
        _logger.LogInformation("Stock set to {Stock} for ProductId {ProductId}", dto.StockQuantity, productId);
        return Ok(result);
    }
}
