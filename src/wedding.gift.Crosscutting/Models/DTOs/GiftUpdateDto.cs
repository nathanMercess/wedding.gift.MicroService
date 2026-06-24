using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class GiftUpdateDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "O preço deve ser maior ou igual a zero.")]
    public decimal Price { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O total deve ser maior ou igual a zero.")]
    public decimal Total { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da imagem deve ter no máximo 500 caracteres.")]
    public string Image { get; set; } = string.Empty;

    [MaxLength(80, ErrorMessage = "A categoria deve ter no máximo 80 caracteres.")]
    public string? Category { get; set; }

    public bool Available { get; set; }
    public bool AllowPartialContribution { get; set; } = true;

    [Range(0, 99.99, ErrorMessage = "A taxa do cartÃ£o deve estar entre 0 e 99,99%.")]
    public decimal CreditCardFeePercent { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "O mÃ¡ximo de parcelas no cartÃ£o deve ser maior ou igual a 1.")]
    public int CreditCardMaxInstallments { get; set; } = 12;
}
