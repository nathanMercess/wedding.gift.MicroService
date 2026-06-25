using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<LoginResponseDto> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        LoginResponseDto response = await authService.LoginAsync(dto, cancellationToken);
        return response;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<RegisterResponseDto> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        RegisterResponseDto response = await authService.RegisterAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return response;
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task ConfirmEmail([FromQuery] string email, [FromQuery] string token, CancellationToken cancellationToken)
    {
        await authService.ConfirmEmailAsync(new ConfirmEmailRequestDto { Email = email, Token = token }, cancellationToken);
    }
}
