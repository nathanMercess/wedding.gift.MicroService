using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.SuperAdmin)]
[Route("admin/dashboard")]
public sealed class AdminDashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet]
    public async Task<DashboardResponseDto> Get([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
    {
        DashboardResponseDto dashboard = await dashboardService.GetAsync(query, cancellationToken);
        return dashboard;
    }
}
