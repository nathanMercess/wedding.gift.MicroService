namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardCategoryChartDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
}
