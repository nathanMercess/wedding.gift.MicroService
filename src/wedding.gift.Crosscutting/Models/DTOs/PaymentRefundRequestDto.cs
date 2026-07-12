using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentRefundRequestDto
{
    [Range(typeof(decimal), "0.01", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? Amount { get; set; }
}
