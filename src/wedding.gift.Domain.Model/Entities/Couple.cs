namespace wedding.gift.Domain.Model.Entities;

public class Couple
{
    public Guid Id { get; set; }
    public string Names { get; set; } = string.Empty;
    public DateTime WeddingDate { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
