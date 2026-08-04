using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminMemberOrSuperAdmin)]
[Route("admin/gifts")]
public sealed class AdminGiftsController(IGiftService giftService, IGiftEnrichService giftEnrichService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<GiftResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<GiftResponseDto>> GetAll(
        [FromQuery] GiftQueryParams query,
        CancellationToken cancellationToken)
        => await giftService.GetAllAdminAsync(query, cancellationToken);

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponseDto<GiftResponseDto>), StatusCodes.Status201Created)]
    public async Task<GiftResponseDto> Create([FromBody] GiftCreateDto dto, CancellationToken cancellationToken)
    {
        GiftResponseDto created = await giftService.CreateAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return created;
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<GiftResponseDto>), StatusCodes.Status200OK)]
    public async Task<GiftResponseDto> Update(Guid id, [FromBody] GiftUpdateDto dto, CancellationToken cancellationToken)
        => await giftService.UpdateAsync(id, dto, cancellationToken);

    [HttpPatch("category")]
    [ProducesResponseType(typeof(ApiResponseDto<GiftCategoryBatchUpdateResponseDto>), StatusCodes.Status200OK)]
    public async Task<GiftCategoryBatchUpdateResponseDto> UpdateCategories([FromBody] GiftCategoryBatchUpdateDto dto, CancellationToken cancellationToken)
        => await giftService.UpdateCategoriesAsync(dto, cancellationToken);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await giftService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("enrich")]
    [ProducesResponseType(typeof(ApiResponseDto<GiftEnrichResponseDto>), StatusCodes.Status200OK)]
    public async Task<GiftEnrichResponseDto> Enrich([FromBody] GiftEnrichRequestDto dto, CancellationToken cancellationToken)
        => await giftEnrichService.EnrichAsync(dto.Url, cancellationToken);
}
