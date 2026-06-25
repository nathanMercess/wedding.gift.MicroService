namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPaymentActivityDto
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string ContributorName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDetail { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string MpOrderId { get; set; } = string.Empty;
    public string MpPaymentId { get; set; } = string.Empty;
    public bool ContributionCreated { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
