using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class CoupleController(ICoupleService coupleService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<CoupleResponseDto>), StatusCodes.Status200OK)]
    public async Task<CoupleResponseDto> Get(CancellationToken cancellationToken)
        => await coupleService.GetAsync(cancellationToken);
}
