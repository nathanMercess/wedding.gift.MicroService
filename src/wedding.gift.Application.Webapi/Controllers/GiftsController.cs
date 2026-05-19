using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
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
        [FromQuery] bool? available,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var gifts = await giftService.GetAllAsync(category, available, page, pageSize, cancellationToken);
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

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPost]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GiftResponseDto>> Create([FromBody] GiftCreateDto dto, CancellationToken cancellationToken)
    {
        var created = await giftService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponseDto());
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GiftResponseDto>> Update(Guid id, [FromBody] GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await giftService.UpdateAsync(id, dto, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpPatch("{id:guid}/availability")]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GiftResponseDto>> UpdateAvailability(Guid id, [FromBody] GiftAvailabilityUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await giftService.UpdateAvailabilityAsync(id, dto.Available, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [Authorize(Roles = UserRoles.Admin)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await giftService.DeleteAsync(id, cancellationToken);
        return NoContent();
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
