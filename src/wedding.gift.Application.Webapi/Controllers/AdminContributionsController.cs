using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/contributions")]
public sealed class AdminContributionsController(IContributionService contributionService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<ContributionResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<ContributionResponseDto>> GetAll(
        [FromQuery] ContributionAdminQueryParams query,
        CancellationToken cancellationToken)
        => await contributionService.GetAllAdminAsync(query, cancellationToken);

    [HttpGet("export.csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ContributionAdminQueryParams query, CancellationToken cancellationToken)
    {
        byte[] content = await contributionService.ExportCsvAsync(query, cancellationToken);
        return File(content, "text/csv; charset=utf-8", $"contribuicoes-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    [HttpPatch("{id:guid}/message-read")]
    [ProducesResponseType(typeof(ApiResponseDto<ContributionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ContributionResponseDto> SetMessageRead(Guid id, [FromBody] ContributionMessageReadUpdateDto dto, CancellationToken cancellationToken)
        => await contributionService.SetMessageReadAsync(id, dto.Read, cancellationToken);

    [HttpPatch("{id:guid}/message-archive")]
    [ProducesResponseType(typeof(ApiResponseDto<ContributionResponseDto>), StatusCodes.Status200OK)]
    public async Task<ContributionResponseDto> SetMessageArchived(Guid id, [FromBody] ContributionMessageArchiveUpdateDto dto, CancellationToken cancellationToken)
        => await contributionService.SetMessageArchivedAsync(id, dto.Archived, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] ContributionStatusUpdateDto dto,
        CancellationToken cancellationToken)
    {
        await contributionService.UpdateStatusAsync(id, dto.Status, dto.PaidAt ?? DateTime.UtcNow, cancellationToken);
        return NoContent();
    }
}
