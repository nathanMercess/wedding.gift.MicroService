namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPaymentFailureReasonDto
{
    public string StatusDetail { get; set; } = string.Empty;
    public int Count { get; set; }
}
