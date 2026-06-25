namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardStatusChartDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
