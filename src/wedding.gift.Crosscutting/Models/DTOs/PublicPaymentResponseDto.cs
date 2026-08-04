namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PublicPaymentResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public Guid? GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusDetail { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool? ContributionCreated { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal? RemainingAmount { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string? QrCodeBase64 { get; set; }

    public string PixQrCode
    {
        get => QrCode;
        set => QrCode = value;
    }
}
