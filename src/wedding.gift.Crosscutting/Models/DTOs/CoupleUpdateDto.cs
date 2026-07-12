using System.ComponentModel.DataAnnotations;
using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CoupleUpdateDto
{
    [MaxLength(200, ErrorMessage = "Os nomes devem ter no máximo 200 caracteres.")]
    public string? Names { get; set; }

    public DateTime? WeddingDate { get; set; }

    [MaxLength(500, ErrorMessage = "A URL da foto deve ter no máximo 500 caracteres.")]
    public string? PhotoUrl { get; set; }

    [MaxLength(1000, ErrorMessage = "A mensagem deve ter no máximo 1000 caracteres.")]
    public string? Message { get; set; }

    [MaxLength(500, ErrorMessage = "A localizacao do evento deve ter no maximo 500 caracteres.")]
    public string? EventLocation { get; set; }

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor primária deve ser um código hexadecimal válido.")]
    public string? PrimaryColor { get; set; }

    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "A cor secundária deve ser um código hexadecimal válido.")]
    public string? SecondaryColor { get; set; }

    [RegularExpression("^(Traditional|PrivateUnlimited)$", ErrorMessage = "O modo de exibição é inválido.")]
    public string? GiftDisplayMode { get; set; }

    public List<CarouselPhotoDto>? CarouselPhotos { get; set; }

    public SiteSettingsUpdateDto? SiteSettings { get; set; }
}
