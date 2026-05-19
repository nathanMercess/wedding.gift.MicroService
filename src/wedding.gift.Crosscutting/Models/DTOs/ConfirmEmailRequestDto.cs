using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class ConfirmEmailRequestDto
{
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [MaxLength(160, ErrorMessage = "O e-mail deve ter no máximo 160 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O token é obrigatório.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "O token deve conter 6 dígitos.")]
    public string Token { get; set; } = string.Empty;
}
