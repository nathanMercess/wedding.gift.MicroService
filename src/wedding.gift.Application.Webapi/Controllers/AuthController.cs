using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.RegisterAsync(dto, cancellationToken);
        return Accepted(new { message = "Cadastro realizado. Confira seu e-mail para confirmar a conta." });
    }

    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ConfirmEmailAsync(dto, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationEmailRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ResendConfirmationEmailAsync(dto, cancellationToken);
        return Accepted(new { message = "Se o e-mail existir e ainda não estiver confirmado, um novo token será enviado." });
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthUserResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var user = await authService.LoginAsync(dto, cancellationToken);
        return Ok(user);
    }
}
