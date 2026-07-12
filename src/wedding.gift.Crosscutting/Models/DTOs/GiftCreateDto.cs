using System.ComponentModel.DataAnnotations;
using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "O preço deve ser maior que zero.", ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "O total deve ser maior que zero.", ParseLimitsInInvariantCulture = true)]
    public decimal Total { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da imagem deve ter no máximo 500 caracteres.")]
    public string Image { get; set; } = string.Empty;

    [MaxLength(80, ErrorMessage = "A categoria deve ter no máximo 80 caracteres.")]
    public string? Category { get; set; }

    public bool AllowPartialContribution { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Category) && !GiftCategories.IsValid(Category))
            yield return new ValidationResult("Categoria invalida.", [nameof(Category)]);
    }
}
