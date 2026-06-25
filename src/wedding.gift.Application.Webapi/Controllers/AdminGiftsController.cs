using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/gifts")]
public sealed class AdminGiftsController(IGiftService giftService, IGiftEnrichService giftEnrichService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<GiftResponseDto>>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
    {
        PagedResult<GiftResponseDto> result = await giftService.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<GiftResponseDto>> Create([FromBody] GiftCreateDto dto, CancellationToken cancellationToken)
    {
        Gift created = await giftService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created.ToResponseDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GiftResponseDto>> Update(Guid id, [FromBody] GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        Gift updated = await giftService.UpdateAsync(id, dto, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<ActionResult<GiftResponseDto>> UpdateAvailability(Guid id, [FromBody] GiftAvailabilityUpdateDto dto, CancellationToken cancellationToken)
    {
        Gift updated = await giftService.UpdateAvailabilityAsync(id, dto.Available, cancellationToken);
        return Ok(updated.ToResponseDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await giftService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost("enrich")]
    public async Task<ActionResult<GiftEnrichResponseDto>> Enrich([FromBody] GiftEnrichRequestDto dto, CancellationToken cancellationToken)
    {
        GiftEnrichResponseDto result = await giftEnrichService.EnrichAsync(dto.Url, cancellationToken);
        return Ok(result);
    }
}
