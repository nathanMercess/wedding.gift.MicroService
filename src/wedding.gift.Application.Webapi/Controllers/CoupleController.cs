using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class CoupleController(ICoupleService coupleService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(CoupleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoupleResponseDto>> Get(CancellationToken cancellationToken)
    {
        Couple couple = await coupleService.GetAsync(cancellationToken);

        if (couple is null) return Ok(new CoupleResponseDto());

        return Ok(couple.ToResponseDto());
    }
}
