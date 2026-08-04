using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/guests")]
public sealed class AdminGuestsController(IGuestService guestService) : ApiControllerBase
{
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<GuestSummaryDto> GetSummary(CancellationToken cancellationToken)
        => await guestService.GetSummaryAsync(cancellationToken);

    [HttpGet("invitations")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<GuestInvitationResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<GuestInvitationResponseDto>> GetInvitations([FromQuery] GuestInvitationQueryDto query, CancellationToken cancellationToken)
        => await guestService.GetInvitationsAsync(query, cancellationToken);

    [HttpGet("invitations/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestInvitationResponseDto>), StatusCodes.Status200OK)]
    public async Task<GuestInvitationResponseDto> GetInvitationById(Guid id, CancellationToken cancellationToken)
        => await guestService.GetInvitationByIdAsync(id, cancellationToken);

    [HttpPost("invitations")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestInvitationResponseDto>), StatusCodes.Status201Created)]
    public async Task<GuestInvitationResponseDto> CreateInvitation([FromBody] GuestInvitationCreateDto dto, CancellationToken cancellationToken)
    {
        GuestInvitationResponseDto result = await guestService.CreateInvitationAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return result;
    }

    [HttpPut("invitations/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestInvitationResponseDto>), StatusCodes.Status200OK)]
    public async Task<GuestInvitationResponseDto> UpdateInvitation(Guid id, [FromBody] GuestInvitationUpdateDto dto, CancellationToken cancellationToken)
        => await guestService.UpdateInvitationAsync(id, dto, cancellationToken);

    [HttpPatch("invitations/{id:guid}/active")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestInvitationResponseDto>), StatusCodes.Status200OK)]
    public async Task<GuestInvitationResponseDto> SetInvitationActive(Guid id, [FromBody] GuestInvitationActiveUpdateDto dto, CancellationToken cancellationToken)
        => await guestService.SetInvitationActiveAsync(id, dto.IsActive, cancellationToken);

    [HttpDelete("invitations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteInvitation(Guid id, CancellationToken cancellationToken)
    {
        await guestService.DeleteInvitationAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/import")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestInvitationImportResponseDto>), StatusCodes.Status200OK)]
    public async Task<GuestInvitationImportResponseDto> ImportInvitations(
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        await using Stream stream = file.OpenReadStream();
        return await guestService.ImportInvitationsAsync(stream, cancellationToken);
    }

    [HttpGet("confirmations")]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<GuestConfirmationResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<GuestConfirmationResponseDto>> GetConfirmations([FromQuery] GuestConfirmationQueryDto query, CancellationToken cancellationToken)
        => await guestService.GetConfirmationsAsync(query, cancellationToken);

    [HttpPut("confirmations/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<GuestConfirmationResponseDto>), StatusCodes.Status200OK)]
    public async Task<GuestConfirmationResponseDto> UpdateConfirmation(Guid id, [FromBody] GuestConfirmationCreateDto dto, CancellationToken cancellationToken)
        => await guestService.UpdateConfirmationAsync(id, dto, cancellationToken);

    [HttpDelete("confirmations/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteConfirmation(Guid id, CancellationToken cancellationToken)
    {
        await guestService.DeleteConfirmationAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("confirmations/export.csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportConfirmations([FromQuery] GuestConfirmationQueryDto query, CancellationToken cancellationToken)
    {
        byte[] content = await guestService.ExportConfirmationsCsvAsync(query, cancellationToken);
        return File(content, "text/csv; charset=utf-8", $"confirmacoes-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }
}
