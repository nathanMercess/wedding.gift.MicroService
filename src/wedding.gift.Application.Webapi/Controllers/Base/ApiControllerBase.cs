using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Application.Webapi.Controllers.Base;

[ApiController]
[Authorize]
[Route("[controller]")]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status500InternalServerError)]
public abstract class ApiControllerBase : ControllerBase
{
}
