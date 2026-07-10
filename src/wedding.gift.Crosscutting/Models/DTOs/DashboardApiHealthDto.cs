namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardApiHealthDto
{
    public decimal SuccessRate { get; set; }
    public int ServerErrors { get; set; }
    public int ClientErrors { get; set; }
    public int SlowRequests { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public decimal P95DurationMilliseconds { get; set; }
    public List<DashboardApiEndpointHealthDto> SlowestEndpoints { get; set; } = [];
    public List<DashboardApiEndpointHealthDto> TopErrorEndpoints { get; set; } = [];
    public DateTime? LastServerErrorAtUtc { get; set; }
}
