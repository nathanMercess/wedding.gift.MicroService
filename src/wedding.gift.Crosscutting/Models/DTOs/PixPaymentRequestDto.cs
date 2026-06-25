namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PixPaymentRequestDto
{
    public required Guid GiftId { get; set; }
    public required string ContributorName { get; set; }
    public string Message { get; set; } = string.Empty;
    public required string OrderId { get; set; }
    public decimal Amount { get; set; }
    public required string PayerEmail { get; set; }
    public string PayerDocType { get; set; } = "CPF";
    public required string PayerDocNumber { get; set; }
}
