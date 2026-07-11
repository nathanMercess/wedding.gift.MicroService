namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentReconciliationItemDto
{
    public string MpOrderId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool ContributionCreated { get; set; }
    public string Result { get; set; } = string.Empty;
}
