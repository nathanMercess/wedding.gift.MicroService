namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CoupleOverviewDto
{
    public decimal TotalRaised { get; set; }
    public decimal Goal { get; set; }
    public int TotalGifts { get; set; }
    public int CompletedGifts { get; set; }
    public int GiftsWithoutContribution { get; set; }
    public int ApprovedContributions { get; set; }
    public int PendingContributions { get; set; }
    public int FailedContributions { get; set; }
    public int UniqueContributors { get; set; }
    public List<DailyApprovedAmountDto> DailyApprovedAmounts { get; set; } = [];
}

public sealed class DailyApprovedAmountDto
{
    public DateTime DateUtc { get; set; }
    public decimal Amount { get; set; }
}
