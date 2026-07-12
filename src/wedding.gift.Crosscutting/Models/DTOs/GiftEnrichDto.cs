using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftEnrichRequestDto
{
    [Required, Url, MaxLength(2048)]
    public string Url { get; set; } = string.Empty;
}
