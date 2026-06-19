namespace wedding.gift.Crosscutting.Models.DTOs;

public class PaymentResponseDto
{
    public required string Status { get; set; }
    public string? StatusDetail { get; set; }
    public string? ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Nsu { get; set; } = string.Empty;
    public string? MpOrderId { get; set; }
    public string? MpPaymentId { get; set; }

    /// <summary>x-request-id da resposta do Mercado Pago — usar em chamados de suporte.</summary>
    public string? MpRequestId { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string? QrCodeBase64 { get; set; }

    // Mantém PixQrCode para compatibilidade com código existente
    public string PixQrCode
    {
        get => QrCode;
        set => QrCode = value;
    }
}
