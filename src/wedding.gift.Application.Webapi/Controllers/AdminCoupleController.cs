using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/couple")]
public class AdminCoupleController(ICoupleService coupleService) : ApiControllerBase
{
    [HttpPut]
    [ProducesResponseType(typeof(CoupleResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CoupleResponseDto>> Update([FromBody] CoupleUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await coupleService.UpdateAsync(dto, cancellationToken);
        return Ok(updated.ToResponseDto());
    }
}
