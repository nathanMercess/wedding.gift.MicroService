namespace wedding.gift.Crosscutting.Models.DTOs;

public class PaymentWebhookResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}
