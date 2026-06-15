namespace wedding.gift.Crosscutting.Models.DTOs;

public class PaymentResponseDto
{
    public required string Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Nsu { get; set; } = string.Empty;
    public string PixQrCode { get; set; } = string.Empty;
}
