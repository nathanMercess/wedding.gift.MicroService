using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/dashboard")]
public sealed class AdminDashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardResponseDto>), StatusCodes.Status200OK)]
    public async Task<DashboardResponseDto> Get([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetAsync(query, cancellationToken);

    [HttpGet("overview")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardOverviewResponseDto>), StatusCodes.Status200OK)]
    public async Task<DashboardOverviewResponseDto> GetOverview([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetOverviewAsync(query, cancellationToken);

    [HttpGet("charts")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardChartsDto>), StatusCodes.Status200OK)]
    public async Task<DashboardChartsDto> GetCharts([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetChartsAsync(query, cancellationToken);

    [HttpGet("action-center")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardActionCenterDto>), StatusCodes.Status200OK)]
    public async Task<DashboardActionCenterDto> GetActionCenter([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetActionCenterAsync(query, cancellationToken);

    [HttpGet("revenue")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardRevenueDto>), StatusCodes.Status200OK)]
    public async Task<DashboardRevenueDto> GetRevenue([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetRevenueAsync(query, cancellationToken);

    [HttpGet("payment-health")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardPaymentHealthDto>), StatusCodes.Status200OK)]
    public async Task<DashboardPaymentHealthDto> GetPaymentHealth([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetPaymentHealthAsync(query, cancellationToken);

    [HttpGet("gift-insights")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardGiftInsightsDto>), StatusCodes.Status200OK)]
    public async Task<DashboardGiftInsightsDto> GetGiftInsights([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetGiftInsightsAsync(query, cancellationToken);

    [HttpGet("api-health")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<DashboardApiHealthDto>), StatusCodes.Status200OK)]
    public async Task<DashboardApiHealthDto> GetApiHealth([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetApiHealthAsync(query, cancellationToken);

    [HttpGet("activity-feed")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<List<DashboardActivityFeedItemDto>>), StatusCodes.Status200OK)]
    public async Task<List<DashboardActivityFeedItemDto>> GetActivityFeed([FromQuery] DashboardQueryDto query, CancellationToken cancellationToken)
        => await dashboardService.GetActivityFeedAsync(query, cancellationToken);
}
