using System.ComponentModel.DataAnnotations;
using wedding.gift.Crosscutting.Validation;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PixPaymentRequestDto
{
    [Required]
    public required Guid GiftId { get; set; }
    [Required, MaxLength(120)]
    public required string ContributorName { get; set; }
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
    [Required, MaxLength(100), RegularExpression("^[0-9a-fA-F-]{36}$")]
    public required string OrderId { get; set; }
    [Range(typeof(decimal), "10.00", "99999999.99", ParseLimitsInInvariantCulture = true)]
    [DecimalScale(2)]
    public decimal Amount { get; set; }
    [Required, EmailAddress, MaxLength(180)]
    public required string PayerEmail { get; set; }
    [Required, MaxLength(20)]
    public string PayerDocType { get; set; } = "CPF";
    [Required, MaxLength(30), RegularExpression(@"^[0-9./-]+$")]
    public required string PayerDocNumber { get; set; }
}
