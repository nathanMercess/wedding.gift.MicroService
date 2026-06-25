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
    public async Task<PagedResult<GiftResponseDto>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
    {
        PagedResult<GiftResponseDto> result = await giftService.GetAllAsync(query, cancellationToken);
        return result;
    }

    [HttpPost]
    public async Task<GiftResponseDto> Create([FromBody] GiftCreateDto dto, CancellationToken cancellationToken)
    {
        Gift created = await giftService.CreateAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return created.ToResponseDto();
    }

    [HttpPut("{id:guid}")]
    public async Task<GiftResponseDto> Update(Guid id, [FromBody] GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        Gift updated = await giftService.UpdateAsync(id, dto, cancellationToken);
        return updated.ToResponseDto();
    }

    [HttpPatch("{id:guid}/availability")]
    public async Task<GiftResponseDto> UpdateAvailability(Guid id, [FromBody] GiftAvailabilityUpdateDto dto, CancellationToken cancellationToken)
    {
        Gift updated = await giftService.UpdateAvailabilityAsync(id, dto.Available, cancellationToken);
        return updated.ToResponseDto();
    }

    [HttpDelete("{id:guid}")]
    public async Task Delete(Guid id, CancellationToken cancellationToken)
    {
        await giftService.DeleteAsync(id, cancellationToken);
    }

    [HttpPost("enrich")]
    public async Task<GiftEnrichResponseDto> Enrich([FromBody] GiftEnrichRequestDto dto, CancellationToken cancellationToken)
    {
        GiftEnrichResponseDto result = await giftEnrichService.EnrichAsync(dto.Url, cancellationToken);
        return result;
    }
}
