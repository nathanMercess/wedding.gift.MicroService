namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentResponseDto
{
    public required string Status { get; set; }
    public string? StatusDetail { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Nsu { get; set; } = string.Empty;
    public string? MpOrderId { get; set; }
    public string? MpPaymentId { get; set; }
    public bool? ContributionCreated { get; set; }

    public string? MpRequestId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string? QrCodeBase64 { get; set; }

    public string PixQrCode
    {
        get => QrCode;
        set => QrCode = value;
    }
}
