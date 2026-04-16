namespace wedding.gift.Application.Webapi.Models.DTOs;

public class ContributionResponseDto
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaidAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
