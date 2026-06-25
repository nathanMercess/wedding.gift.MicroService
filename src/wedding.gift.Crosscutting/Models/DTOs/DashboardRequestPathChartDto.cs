namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardRequestPathChartDto
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ServerErrors { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public long MaxDurationMilliseconds { get; set; }
}
