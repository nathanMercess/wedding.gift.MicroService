using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public class ConfirmEmailRequestDto
{
    [Required(ErrorMessage = "O email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O token é obrigatório.")]
    public string Token { get; set; } = string.Empty;
}
