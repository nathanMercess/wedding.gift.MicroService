namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftEnrichResponseDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
}
