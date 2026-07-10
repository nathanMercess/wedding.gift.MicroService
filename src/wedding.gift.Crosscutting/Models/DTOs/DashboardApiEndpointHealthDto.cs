namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardApiEndpointHealthDto
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ServerErrors { get; set; }
    public int ClientErrors { get; set; }
    public int SlowRequests { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public decimal P95DurationMilliseconds { get; set; }
    public long MaxDurationMilliseconds { get; set; }
}
