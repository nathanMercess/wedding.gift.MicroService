using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("admin/uploads")]
public class AdminUploadsController(IImageUploadService imageUploadService) : ControllerBase
{
    [HttpPost("image")]
    [ProducesResponseType(typeof(ImageUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImageUploadResponseDto>> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null)
        {
            throw new BadRequestException("Selecione uma imagem para enviar.");
        }

        await using var stream = file.OpenReadStream();
        var url = await imageUploadService.UploadImageAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return Ok(new ImageUploadResponseDto { Url = url });
    }
}
