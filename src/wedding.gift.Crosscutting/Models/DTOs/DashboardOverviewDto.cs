namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardOverviewDto
{
    public decimal TotalRaised { get; set; }
    public decimal TotalGoal { get; set; }
    public decimal FundingPercent { get; set; }
    public int TotalGifts { get; set; }
    public int FullyFundedGifts { get; set; }
    public int PaidContributions { get; set; }
    public int UniqueContributors { get; set; }
    public int ApprovedPayments { get; set; }
    public int FailedPayments { get; set; }
}
