using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
[Route("guest-confirmations")]
public sealed class GuestConfirmationsController(IGuestService guestService) : ApiControllerBase
{
    [HttpGet("suggestions")]
    [EnableRateLimiting("guest-search")]
    [ProducesResponseType(typeof(ApiResponseDto<IReadOnlyList<GuestSuggestionDto>>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<GuestSuggestionDto>> GetSuggestions(
        [FromQuery, Required, MinLength(3), MaxLength(120)] string search,
        CancellationToken cancellationToken)
        => await guestService.GetSuggestionsAsync(search, cancellationToken);

    [HttpPost]
    [EnableRateLimiting("public-write")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestConfirmationResponseDto>), StatusCodes.Status201Created)]
    public async Task<GuestConfirmationResponseDto> Create(
        [FromBody] GuestConfirmationCreateDto dto,
        CancellationToken cancellationToken)
    {
        GuestConfirmationResponseDto created = await guestService.CreateConfirmationAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return created;
    }
}
