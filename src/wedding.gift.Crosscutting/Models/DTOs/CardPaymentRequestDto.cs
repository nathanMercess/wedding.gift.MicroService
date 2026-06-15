namespace wedding.gift.Crosscutting.Models.DTOs;

public class CardPaymentRequestDto
{
    public required string CardToken { get; set; }
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public required string Method { get; set; }
}
