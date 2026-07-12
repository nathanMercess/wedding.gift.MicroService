using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class ConfirmEmailRequestDto
{
    [Required(ErrorMessage = "O email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    [MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O token é obrigatório.")]
    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;
}
