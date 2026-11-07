using BffService.Composers;
using BffService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BffService.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ProductWithOrdersComposer _composer;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ProductWithOrdersComposer composer, ILogger<DashboardController> logger)
    {
        _composer = composer;
        _logger = logger;
    }

    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(UserDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDashboardDto>> GetUserDashboard(int userId)
    {
        var dashboard = await _composer.ComposeUserDashboardAsync(userId);

        if (dashboard == null)
            return NotFound(new { message = $"User with ID {userId} not found." });

        return Ok(dashboard);
    }

    [HttpGet("catalog")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CatalogPageDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CatalogPageDto>> GetCatalogPage(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var catalog = await _composer.ComposeCatalogPageAsync(pageNumber, pageSize);
        return Ok(catalog);
    }
}
