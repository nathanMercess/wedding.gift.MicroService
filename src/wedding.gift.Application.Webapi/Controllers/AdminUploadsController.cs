using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminMemberOrSuperAdmin)]
[Route("admin/uploads")]
public sealed class AdminUploadsController(IImageUploadService imageUploadService) : ApiControllerBase
{
    [HttpPost("image")]
    [ProducesResponseType(typeof(ApiResponseDto<ImageUploadResponseDto>), StatusCodes.Status200OK)]
    public async Task<ImageUploadResponseDto> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null)
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        await using Stream stream = file.OpenReadStream();
        string url = await imageUploadService.UploadImageAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return new ImageUploadResponseDto { Url = url };
    }

    [HttpDelete("image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteImage(
        [FromBody] ImageDeleteRequestDto dto,
        CancellationToken cancellationToken)
    {
        await imageUploadService.DeleteImageAsync(dto.Url, cancellationToken);
        return NoContent();
    }
}
