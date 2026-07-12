namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class AdminPaymentResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDetail { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public bool ContributionCreated { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
