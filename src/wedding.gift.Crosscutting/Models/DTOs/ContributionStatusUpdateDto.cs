using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ContributionStatusUpdateDto
{
    [Required]
    [RegularExpression("^(Pending|Paid|Cancelled|Refunded|Chargeback)$")]
    public string Status { get; set; } = string.Empty;

    public DateTime? PaidAt { get; set; }
}
