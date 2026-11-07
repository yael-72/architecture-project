using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductCatalogService.DTOs;
using ProductCatalogService.Interfaces;
using SharedKernel.DTOs;

namespace ProductCatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductService productService, ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetAll()
    {
        // X-Instance-Id proves which replica handled this request (used for load-balancing demo)
        Response.Headers["X-Instance-Id"] = Environment.MachineName;
        var products = await _productService.GetAllProductsAsync();
        return Ok(products);
    }

    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> GetAllPaged([FromQuery] PaginationParams paginationParams)
    {
        var result = await _productService.GetAllProductsPagedAsync(paginationParams);
        return Ok(result);
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

    [HttpGet("category/{categoryId}")]
    [ProducesResponseType(typeof(IEnumerable<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> GetByCategory(int categoryId)
    {
        var products = await _productService.GetProductsByCategoryAsync(categoryId);
        return Ok(products);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponseDto>>> Search([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Search term cannot be empty." });
        var products = await _productService.SearchProductsByNameAsync(name);
        return Ok(products);
    }

    [HttpGet("search/paged")]
    [ProducesResponseType(typeof(PagedResult<ProductResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponseDto>>> SearchPaged([FromQuery] string name, [FromQuery] PaginationParams paginationParams)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Search term cannot be empty." });
        var result = await _productService.SearchProductsByNamePagedAsync(name, paginationParams);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Manager,Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponseDto>> Create([FromBody] ProductCreateDto createDto)
    {
        try
        {
            var product = await _productService.CreateProductAsync(createDto);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Manager,Admin")]
    [ProducesResponseType(typeof(ProductResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponseDto>> Update(int id, [FromBody] ProductUpdateDto updateDto)
    {
        try
        {
            var product = await _productService.UpdateProductAsync(id, updateDto);
            if (product == null)
                return NotFound(new { message = $"Product with ID {id} not found." });
            return Ok(product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result)
            return NotFound(new { message = $"Product with ID {id} not found." });
        return NoContent();
    }
}
