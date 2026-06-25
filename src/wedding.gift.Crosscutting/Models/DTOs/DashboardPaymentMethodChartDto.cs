namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPaymentMethodChartDto
{
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}
