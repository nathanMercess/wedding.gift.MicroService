namespace wedding.gift.Crosscutting.Models.DTOs;

public class PixPaymentRequestDto
{
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public required string PayerEmail { get; set; }
    public string PayerDocType { get; set; } = "CPF";
    public required string PayerDocNumber { get; set; }
}
