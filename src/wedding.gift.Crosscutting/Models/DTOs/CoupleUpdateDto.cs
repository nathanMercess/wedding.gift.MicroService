using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class CoupleUpdateDto
{
    [Required(ErrorMessage = "Os nomes são obrigatórios.")]
    [MaxLength(200, ErrorMessage = "Os nomes devem ter no máximo 200 caracteres.")]
    public string Names { get; set; } = string.Empty;

    public DateTime WeddingDate { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da foto deve ter no máximo 500 caracteres.")]
    public string PhotoUrl { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "A mensagem deve ter no máximo 1000 caracteres.")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(7, ErrorMessage = "A cor primária deve ser um código hex válido (ex: #C79A6D).")]
    public string PrimaryColor { get; set; } = "#C79A6D";

    [MaxLength(7, ErrorMessage = "A cor secundária deve ser um código hex válido (ex: #F7F0EA).")]
    public string SecondaryColor { get; set; } = "#F7F0EA";
}
