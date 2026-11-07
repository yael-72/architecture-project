using Microsoft.AspNetCore.Mvc;
using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;

namespace ProductCatalogService.Controllers.Internal;

// Internal endpoint consumed only by OrderService via HTTP client.
// Not exposed publicly through the API Gateway.
[ApiController]
[Route("internal/[controller]")]
public class InternalProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<InternalProductsController> _logger;

    public InternalProductsController(IProductService productService, ILogger<InternalProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponseDto>> GetById(int id)
    {
        var product = await _productService.GetProductByIdAsync(id);
        if (product == null)
            return NotFound(new { message = $"Product with ID {id} not found." });
        return Ok(product);
    }

    [HttpPost("deduct-stock")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeductStock([FromBody] StockUpdateDto dto)
    {
        var success = await _productService.DeductStockAsync(dto.ProductId, dto.QuantityChange);
        if (!success)
            return BadRequest(new { message = $"Insufficient stock or product not found for ID {dto.ProductId}." });

        _logger.LogInformation("Stock deducted: Product {ProductId}, Quantity: {Qty}", dto.ProductId, dto.QuantityChange);
        return Ok(new { message = "Stock updated successfully." });
    }
}
