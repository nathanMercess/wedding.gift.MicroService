namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ContributionResponseDto
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string GuestEmail { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime PaidAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? MessageReadAtUtc { get; set; }
    public DateTime? MessageArchivedAtUtc { get; set; }
}
