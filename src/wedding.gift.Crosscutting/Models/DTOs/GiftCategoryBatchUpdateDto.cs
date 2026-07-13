using System.ComponentModel.DataAnnotations;
using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftCategoryBatchUpdateDto : IValidatableObject
{
    [Required(ErrorMessage = "Selecione pelo menos um presente.")]
    [MinLength(1, ErrorMessage = "Selecione pelo menos um presente.")]
    [MaxLength(100, ErrorMessage = "Selecione no máximo 100 presentes por vez.")]
    public List<Guid> GiftIds { get; set; } = [];

    [MaxLength(80, ErrorMessage = "A categoria deve ter no máximo 80 caracteres.")]
    public string? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Category) && !GiftCategories.IsValid(Category))
            yield return new ValidationResult("Categoria inválida.", [nameof(Category)]);
    }
}

public sealed class GiftCategoryBatchUpdateResponseDto
{
    public List<Guid> GiftIds { get; set; } = [];
    public string? Category { get; set; }
}
