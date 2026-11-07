using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;
using OrderService.Interfaces;

namespace OrderService.Controllers.Internal;

// Internal endpoint for BFF or other services to query orders directly.
[ApiController]
[Route("internal/[controller]")]
public class InternalOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<InternalOrdersController> _logger;

    public InternalOrdersController(IOrderService orderService, ILogger<InternalOrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<OrderResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetByUserId(int userId)
    {
        var orders = await _orderService.GetOrdersByUserIdAsync(userId);
        return Ok(orders);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponseDto>> GetById(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        if (order == null)
            return NotFound(new { message = $"Order with ID {id} not found." });
        return Ok(order);
    }
}
