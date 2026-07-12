using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CarouselPhotoDto
{
    [Required, Url, MaxLength(500)]
    public string Url { get; set; } = string.Empty;
    [MaxLength(80)]
    public string Tag { get; set; } = string.Empty;
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;
}
