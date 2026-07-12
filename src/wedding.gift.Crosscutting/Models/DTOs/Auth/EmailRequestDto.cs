using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class EmailRequestDto
{
    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;
}
