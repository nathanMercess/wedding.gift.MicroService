namespace wedding.gift.Crosscutting.Models.DTOs;

public class PixPaymentRequestDto
{
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
}
