using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentOrderLookupRequestDto
{
    [Required]
    [MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}
