namespace wedding.gift.Domain.Model.Entities;

public sealed class Couple
{
    public Guid Id { get; set; }
    public string Names { get; set; } = string.Empty;
    public DateTime WeddingDate { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#C79A6D";
    public string SecondaryColor { get; set; } = "#F7F0EA";
    public string? CarouselPhotosJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
