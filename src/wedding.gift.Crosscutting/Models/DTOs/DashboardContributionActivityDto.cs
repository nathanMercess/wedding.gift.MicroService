namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardContributionActivityDto
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string ContributorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
}
