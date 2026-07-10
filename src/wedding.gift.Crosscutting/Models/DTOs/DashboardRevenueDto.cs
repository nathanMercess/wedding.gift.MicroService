namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardRevenueDto
{
    public decimal TotalRaised { get; set; }
    public decimal PeriodRaised { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AverageTicket { get; set; }
    public decimal LargestContribution { get; set; }
    public int PeriodPaidCount { get; set; }
    public decimal FundingPercent { get; set; }
    public decimal DailyAverage { get; set; }
    public decimal BestDayAmount { get; set; }
    public DateTime? BestDayUtc { get; set; }
}
