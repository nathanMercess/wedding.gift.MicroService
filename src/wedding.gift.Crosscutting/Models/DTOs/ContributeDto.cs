using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ContributeDto
{
    [Required(ErrorMessage = "O nome do convidado é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome do convidado deve ter no máximo 120 caracteres.")]
    public string GuestName { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "O valor da contribuição deve ser maior que zero.")]
    public decimal Amount { get; set; }

    [MaxLength(500, ErrorMessage = "A mensagem deve ter no máximo 500 caracteres.")]
    public string? Message { get; set; }
}
