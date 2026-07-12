using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ImageDeleteRequestDto
{
    [Required, Url, MaxLength(500)]
    public string Url { get; set; } = string.Empty;
}
