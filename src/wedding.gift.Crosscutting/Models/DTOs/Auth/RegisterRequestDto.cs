using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class RegisterRequestDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "O email é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um email válido.")]
    [MaxLength(180, ErrorMessage = "O email deve ter no máximo 180 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres.")]
    [MaxLength(128, ErrorMessage = "A senha deve ter no máximo 128 caracteres.")]
    public string Password { get; set; } = string.Empty;
}
