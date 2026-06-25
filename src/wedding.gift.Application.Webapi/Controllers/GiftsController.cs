using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class GiftsController(IGiftService giftService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<PagedResult<GiftResponseDto>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
        => await giftService.GetAllAsync(query, cancellationToken);

    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<GiftStatsDto> GetStats(CancellationToken cancellationToken)
        => await giftService.GetStatsAsync(cancellationToken);

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<GiftResponseDto> GetById(Guid id, CancellationToken cancellationToken)
        => await giftService.GetByIdAsync(id, cancellationToken);

    [AllowAnonymous]
    [HttpPost("{id:guid}/contribute")]
    public async Task<ContributionResponseDto> Contribute(Guid id, [FromBody] ContributeDto dto, CancellationToken cancellationToken)
    {
        ContributionResponseDto contribution = await giftService.ContributeAsync(id, dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return contribution;
    }

    [AllowAnonymous]
    [HttpGet("{giftId:guid}/contributions")]
    public async Task<IEnumerable<ContributionResponseDto>> GetContributionsByGiftId(Guid giftId, CancellationToken cancellationToken)
        => await giftService.GetContributionsByGiftIdAsync(giftId, cancellationToken);
}
