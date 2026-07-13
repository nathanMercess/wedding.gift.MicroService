namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentResponseDto
{
    public string? OrderId { get; set; }
    public Guid? GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string? CurrencyId { get; set; }
    public decimal? RefundedAmount { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public required string Status { get; set; }
    public string? StatusDetail { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Nsu { get; set; } = string.Empty;
    public string? MpOrderId { get; set; }
    public string? MpPaymentId { get; set; }
    public bool? ContributionCreated { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public decimal? RemainingAmount { get; set; }

    public string? MpRequestId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string? QrCodeBase64 { get; set; }

    public string PixQrCode
    {
        get => QrCode;
        set => QrCode = value;
    }
}
