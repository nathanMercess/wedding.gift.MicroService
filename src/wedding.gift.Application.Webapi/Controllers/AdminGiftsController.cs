using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("admin/gifts")]
public class AdminGiftsController(IGiftService giftService) : ControllerBase
{
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

    [HttpPost]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GiftResponseDto>> Create([FromBody] GiftCreateDto dto, CancellationToken cancellationToken)
    {
        var created = await giftService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created.ToResponseDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GiftResponseDto>> Update(Guid id, [FromBody] GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await giftService.UpdateAsync(id, dto, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [HttpPatch("{id:guid}/availability")]
    [ProducesResponseType(typeof(GiftResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GiftResponseDto>> UpdateAvailability(Guid id, [FromBody] GiftAvailabilityUpdateDto dto, CancellationToken cancellationToken)
    {
        var updated = await giftService.UpdateAvailabilityAsync(id, dto.Available, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await giftService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
