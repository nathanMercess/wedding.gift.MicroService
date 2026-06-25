namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardGiftSummaryDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public int Unavailable { get; set; }
    public int FullyFunded { get; set; }
    public int PartiallyFunded { get; set; }
    public int WithoutContributions { get; set; }
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AverageFundingPercent { get; set; }
}
