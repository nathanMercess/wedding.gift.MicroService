using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class GiftsController(IGiftService giftService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<GiftResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<GiftResponseDto>> GetAll([FromQuery] GiftQueryParams query, CancellationToken cancellationToken)
        => await giftService.GetPublicAsync(query, cancellationToken);

    [AllowAnonymous]
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponseDto<GiftStatsDto>), StatusCodes.Status200OK)]
    public async Task<GiftStatsDto> GetStats(CancellationToken cancellationToken)
        => await giftService.GetStatsAsync(cancellationToken);

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<GiftResponseDto>), StatusCodes.Status200OK)]
    public async Task<GiftResponseDto> GetById(Guid id, CancellationToken cancellationToken)
        => await giftService.GetByIdAsync(id, cancellationToken);

    [AllowAnonymous]
    [EnableRateLimiting("public-write")]
    [HttpPost("{id:guid}/contribute")]
    [ProducesResponseType(typeof(ApiResponseDto<ContributionResponseDto>), StatusCodes.Status201Created)]
    public async Task<ContributionResponseDto> Contribute(Guid id, [FromBody] ContributeDto dto, CancellationToken cancellationToken)
    {
        ContributionResponseDto contribution = await giftService.ContributeAsync(id, dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return contribution;
    }

    [AllowAnonymous]
    [HttpGet("{giftId:guid}/contributions")]
    [ProducesResponseType(typeof(ApiResponseDto<IEnumerable<ContributionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IEnumerable<ContributionResponseDto>> GetContributionsByGiftId(Guid giftId, CancellationToken cancellationToken)
        => await giftService.GetContributionsByGiftIdAsync(giftId, cancellationToken);
}
