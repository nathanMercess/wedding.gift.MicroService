namespace wedding.gift.Crosscutting.Models.DTOs;

public class CoupleResponseDto
{
    public Guid Id { get; set; }
    public string Names { get; set; } = string.Empty;
    public DateTime WeddingDate { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#C79A6D";
    public string SecondaryColor { get; set; } = "#F7F0EA";
    public List<string> CarouselPhotos { get; set; } = [];
}
