namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardMessageSummaryDto
{
    public int Total { get; set; }
    public int ContributionMessages { get; set; }
    public int PaymentIntentMessages { get; set; }
    public DateTime? LatestMessageAtUtc { get; set; }
}
