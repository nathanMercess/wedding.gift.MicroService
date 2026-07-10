namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardActionCenterDto
{
    public string HealthStatus { get; set; } = "healthy";
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public List<DashboardActionItemDto> Items { get; set; } = [];
}
