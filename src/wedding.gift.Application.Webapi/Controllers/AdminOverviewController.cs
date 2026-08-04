using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/overview")]
public sealed class AdminOverviewController(ICoupleOverviewService overviewService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<CoupleOverviewDto>), StatusCodes.Status200OK)]
    public async Task<CoupleOverviewDto> Get([FromQuery, Range(1, 365)] int days = 30, CancellationToken cancellationToken = default)
        => await overviewService.GetAsync(days, cancellationToken);
}
