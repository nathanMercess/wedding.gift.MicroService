namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPeriodDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Days { get; set; }
}
