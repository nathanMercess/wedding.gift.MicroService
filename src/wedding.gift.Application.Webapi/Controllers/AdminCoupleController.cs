using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminMemberOrSuperAdmin)]
[Route("admin/couple")]
public sealed class AdminCoupleController(ICoupleService coupleService) : ApiControllerBase
{
    [HttpPatch]
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponseDto<CoupleResponseDto>), StatusCodes.Status200OK)]
    public async Task<CoupleResponseDto> Update([FromBody] CoupleUpdateDto dto, CancellationToken cancellationToken)
        => await coupleService.UpdateAsync(dto, cancellationToken);
}
