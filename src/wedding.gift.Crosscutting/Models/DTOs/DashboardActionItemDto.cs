namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardActionItemDto
{
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Metric { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime? CreatedAtUtc { get; set; }
}
