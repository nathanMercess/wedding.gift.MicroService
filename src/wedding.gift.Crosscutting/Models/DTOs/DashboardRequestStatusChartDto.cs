namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardRequestStatusChartDto
{
    public string StatusGroup { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
}
