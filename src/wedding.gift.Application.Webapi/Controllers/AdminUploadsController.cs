using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/uploads")]
public sealed class AdminUploadsController(IImageUploadService imageUploadService) : ApiControllerBase
{
    [HttpPost("image")]
    [ProducesResponseType(typeof(ImageUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImageUploadResponseDto>> UploadImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null) throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        await using Stream stream = file.OpenReadStream();
        string url = await imageUploadService.UploadImageAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return Ok(new ImageUploadResponseDto { Url = url });
    }
}
