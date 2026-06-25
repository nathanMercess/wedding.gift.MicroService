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
    public async Task<ActionResult<PagedResult<GiftResponseDto>>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
    {
        PagedResult<GiftResponseDto> result = await giftService.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("stats")]
    public async Task<ActionResult<GiftStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        GiftStatsDto stats = await giftService.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GiftResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        Gift gift = await giftService.GetByIdAsync(id, cancellationToken);
        return Ok(gift.ToResponseDto());
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/contribute")]
    public async Task<ActionResult<ContributionResponseDto>> Contribute(Guid id, [FromBody] ContributeDto dto, CancellationToken cancellationToken)
    {
        Contribution contribution = await giftService.ContributeAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, contribution.ToResponseDto());
    }

    [AllowAnonymous]
    [HttpGet("{giftId:guid}/contributions")]
    public async Task<ActionResult<IEnumerable<ContributionResponseDto>>> GetContributionsByGiftId(Guid giftId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Contribution> contributions = await giftService.GetContributionsByGiftIdAsync(giftId, cancellationToken);
        return Ok(contributions.Select(x => x.ToResponseDto()));
    }
}
