namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardRequestSummaryDto
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int ClientErrors { get; set; }
    public int ServerErrors { get; set; }
    public int Authenticated { get; set; }
    public int Anonymous { get; set; }
    public int SlowRequests { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal ClientErrorRate { get; set; }
    public decimal ServerErrorRate { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public long MaxDurationMilliseconds { get; set; }
    public DateTime? LastRequestAtUtc { get; set; }
}
