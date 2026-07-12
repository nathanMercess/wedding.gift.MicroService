using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.SuperAdmin)]
[Route("admin/users")]
public sealed class AdminUsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<UserResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<UserResponseDto>> GetAll(
        [FromQuery] UserQueryParams query,
        CancellationToken cancellationToken)
        => await userService.GetAllAsync(query, cancellationToken);

    [HttpPatch("{id:guid}/active")]
    [ProducesResponseType(typeof(ApiResponseDto<UserResponseDto>), StatusCodes.Status200OK)]
    public async Task<UserResponseDto> UpdateActive(
        Guid id,
        [FromBody] UserActiveUpdateDto dto,
        CancellationToken cancellationToken)
        => await userService.UpdateActiveAsync(id, GetCurrentUserId(), dto.IsActive, cancellationToken);

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(ApiResponseDto<UserResponseDto>), StatusCodes.Status200OK)]
    public async Task<UserResponseDto> UpdateRole(
        Guid id,
        [FromBody] UserRoleUpdateDto dto,
        CancellationToken cancellationToken)
        => await userService.UpdateRoleAsync(id, GetCurrentUserId(), dto.Role, cancellationToken);

    private Guid GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new UnauthorizedException(ErrorCodes.UNAUTHORIZED);
    }
}
