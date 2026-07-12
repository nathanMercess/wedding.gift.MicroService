using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class ResetPasswordRequestDto
{
    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
