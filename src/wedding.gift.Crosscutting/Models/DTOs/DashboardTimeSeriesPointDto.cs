namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardTimeSeriesPointDto
{
    public DateTime DateUtc { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
