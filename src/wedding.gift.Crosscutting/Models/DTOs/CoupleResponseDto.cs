namespace wedding.gift.Crosscutting.Models.DTOs;

public class CoupleResponseDto
{
    public Guid Id { get; set; }
    public string Names { get; set; } = string.Empty;
    public DateTime WeddingDate { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
