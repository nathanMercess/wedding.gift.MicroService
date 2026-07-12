namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Total { get; set; }
    public decimal Raised { get; set; }
    public decimal Remaining { get; set; }
    public bool FullyFunded { get; set; }
    public string Image { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Available { get; set; }
    public bool AllowPartialContribution { get; set; }
}
