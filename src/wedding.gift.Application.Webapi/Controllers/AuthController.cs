using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Controllers;

[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponseDto<LoginResponseDto>), StatusCodes.Status200OK)]
    public async Task<LoginResponseDto> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
        => await authService.LoginAsync(dto, cancellationToken);

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponseDto<RegisterResponseDto>), StatusCodes.Status201Created)]
    public async Task<RegisterResponseDto> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        RegisterResponseDto response = await authService.RegisterAsync(dto, cancellationToken);
        Response.StatusCode = StatusCodes.Status201Created;
        return response;
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ConfirmEmailAsync(dto, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResendConfirmation([FromBody] EmailRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ResendConfirmationAsync(dto, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(dto, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await authService.ResetPasswordAsync(dto, cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponseDto<LoginResponseDto>), StatusCodes.Status200OK)]
    public async Task<LoginResponseDto> Refresh(
        [FromBody] RefreshTokenRequestDto dto,
        CancellationToken cancellationToken)
        => await authService.RefreshAsync(dto, cancellationToken);

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequestDto dto,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(dto, cancellationToken);
        return NoContent();
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponseDto<UserResponseDto>), StatusCodes.Status200OK)]
    public async Task<UserResponseDto> GetProfile(CancellationToken cancellationToken)
        => await authService.GetProfileAsync(GetCurrentUserId(), cancellationToken);

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto dto,
        CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(GetCurrentUserId(), dto, cancellationToken);
        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out Guid id)
            ? id
            : throw new UnauthorizedException("UNAUTHORIZED");
    }
}
