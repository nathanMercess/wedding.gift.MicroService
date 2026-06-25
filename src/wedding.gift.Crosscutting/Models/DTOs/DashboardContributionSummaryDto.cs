namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardContributionSummaryDto
{
    public int Total { get; set; }
    public int Paid { get; set; }
    public int Pending { get; set; }
    public int Cancelled { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal AveragePaidAmount { get; set; }
    public int UniqueContributors { get; set; }
    public int MessagesCount { get; set; }
    public int PeriodPaidCount { get; set; }
    public decimal PeriodPaidAmount { get; set; }
}
