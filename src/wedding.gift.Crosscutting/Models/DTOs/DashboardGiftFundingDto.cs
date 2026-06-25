namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardGiftFundingDto
{
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Raised { get; set; }
    public decimal Remaining { get; set; }
    public decimal FundingPercent { get; set; }
    public int PaidContributions { get; set; }
    public bool Available { get; set; }
    public bool FullyFunded { get; set; }
}
