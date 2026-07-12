using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CardPaymentRequestDto
{
    [Required]
    public required Guid GiftId { get; set; }
    [Required, MaxLength(120)]
    public required string ContributorName { get; set; }
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
    [Required, MaxLength(500)]
    public required string CardToken { get; set; }
    [Required, MaxLength(100), RegularExpression("^[0-9a-fA-F-]{36}$")]
    public required string OrderId { get; set; }
    [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Amount { get; set; }
    [Range(1, 12)]
    public int Installments { get; set; }
    [Required, RegularExpression("^(credit_card|debit_card)$")]
    public required string Method { get; set; }
    [Required, MaxLength(50)]
    public required string PaymentMethodId { get; set; }
    [MaxLength(50)]
    public string? IssuerId { get; set; }
    [MaxLength(200)]
    public string? DeviceId { get; set; }
    [Required, EmailAddress, MaxLength(180)]
    public required string PayerEmail { get; set; }
    [Required, MaxLength(20)]
    public string PayerDocType { get; set; } = "CPF";
    [Required, MaxLength(30), RegularExpression(@"^[0-9./-]+$")]
    public required string PayerDocNumber { get; set; }
}
