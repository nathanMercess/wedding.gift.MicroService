using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Application.Webapi.Models.DTOs;

public class GiftUpdateDto
{
    [Required(ErrorMessage = "O título é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O título deve ter no máximo 120 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [MaxLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "O preço deve ser maior ou igual a zero.")]
    public decimal Price { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da imagem deve ter no máximo 500 caracteres.")]
    public string ImageUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "A categoria é obrigatória.")]
    [MaxLength(80, ErrorMessage = "A categoria deve ter no máximo 80 caracteres.")]
    public string Category { get; set; } = string.Empty;

    public bool Available { get; set; }
}
