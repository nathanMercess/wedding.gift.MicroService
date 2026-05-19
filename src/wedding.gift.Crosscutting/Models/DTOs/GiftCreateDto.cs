using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class GiftCreateDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [MaxLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "O preço deve ser maior ou igual a zero.")]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O total deve ser maior ou igual a zero.")]
    public decimal Total { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da imagem deve ter no máximo 500 caracteres.")]
    public string Image { get; set; } = string.Empty;

    [Required(ErrorMessage = "A categoria é obrigatória.")]
    [MaxLength(80, ErrorMessage = "A categoria deve ter no máximo 80 caracteres.")]
    public string Category { get; set; } = string.Empty;

    public bool Available { get; set; } = true;
}
