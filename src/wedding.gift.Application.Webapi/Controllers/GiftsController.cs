using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

public class GiftsController(IGiftService giftService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GiftResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GiftResponseDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] bool? available,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var gifts = await giftService.GetAllAsync(category, search, available, page, pageSize, cancellationToken);
        return Ok(gifts.Select(x => x.ToResponseDto()));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GiftResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var gift = await giftService.GetByIdAsync(id, cancellationToken);
        return Ok(gift.ToResponseDto());
    }

    [AllowAnonymous]
    [HttpPost("{id:guid}/contribute")]
    [ProducesResponseType(typeof(ContributionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContributionResponseDto>> Contribute(Guid id, [FromBody] ContributeDto dto, CancellationToken cancellationToken)
    {
        var contribution = await giftService.ContributeAsync(id, dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, contribution.ToResponseDto());
    }

    [AllowAnonymous]
    [HttpGet("{giftId:guid}/contributions")]
    [ProducesResponseType(typeof(IEnumerable<ContributionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ContributionResponseDto>>> GetContributionsByGiftId(Guid giftId, CancellationToken cancellationToken)
    {
        var contributions = await giftService.GetContributionsByGiftIdAsync(giftId, cancellationToken);
        return Ok(contributions.Select(x => x.ToResponseDto()));
    }
}
