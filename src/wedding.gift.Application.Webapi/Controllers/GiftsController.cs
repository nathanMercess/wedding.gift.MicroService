using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class GiftsController(IGiftService giftService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<PagedResult<GiftResponseDto>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
    {
        PagedResult<GiftResponseDto> result = await giftService.GetAllAsync(query, cancellationToken);
        return result;
    }

    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<GiftStatsDto> GetStats(CancellationToken cancellationToken)
    {
        GiftStatsDto stats = await giftService.GetStatsAsync(cancellationToken);
        return stats;
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<GiftResponseDto> GetById(Guid id, CancellationToken cancellationToken)
    {
        Gift gift = await giftService.GetByIdAsync(id, cancellationToken);
        return gift.ToResponseDto();
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/contribute")]
    public async Task<ContributionResponseDto> Contribute(Guid id, [FromBody] ContributeDto dto, CancellationToken cancellationToken)
    {
        Contribution contribution = await giftService.ContributeAsync(id, dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return contribution.ToResponseDto();
    }

    [AllowAnonymous]
    [HttpGet("{giftId:guid}/contributions")]
    public async Task<IEnumerable<ContributionResponseDto>> GetContributionsByGiftId(Guid giftId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Contribution> contributions = await giftService.GetContributionsByGiftIdAsync(giftId, cancellationToken);
        return contributions.Select(x => x.ToResponseDto());
    }
}
