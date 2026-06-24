using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.SuperAdmin)]
[Route("admin/dashboard")]
public class AdminDashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DashboardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardResponseDto>> Get([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
    {
        var dashboard = await dashboardService.GetAsync(query, cancellationToken);
        return Ok(dashboard);
    }
}
