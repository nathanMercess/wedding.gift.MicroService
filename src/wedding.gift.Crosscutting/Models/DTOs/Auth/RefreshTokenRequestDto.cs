using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class RefreshTokenRequestDto
{
    [Required, MaxLength(200)]
    public string RefreshToken { get; set; } = string.Empty;
}
