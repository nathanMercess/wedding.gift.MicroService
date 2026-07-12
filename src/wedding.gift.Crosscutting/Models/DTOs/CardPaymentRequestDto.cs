namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CardPaymentRequestDto
{
    public required Guid GiftId { get; set; }
    public required string ContributorName { get; set; }
    public string Message { get; set; } = string.Empty;
    public required string CardToken { get; set; }
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public required string Method { get; set; }
    public required string PaymentMethodId { get; set; }
    public string? IssuerId { get; set; }
    public string? DeviceId { get; set; }
    public required string PayerEmail { get; set; }
    public string PayerDocType { get; set; } = "CPF";
    public required string PayerDocNumber { get; set; }
}
