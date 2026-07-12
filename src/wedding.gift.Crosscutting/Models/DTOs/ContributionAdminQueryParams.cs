using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ContributionAdminQueryParams : ContributionQueryParams
{
    [MaxLength(120)]
    public string? Search { get; set; }

    [MaxLength(20)]
    public string? Status { get; set; }

    [MaxLength(30)]
    public string? PaymentMethod { get; set; }

    public bool? HasMessage { get; set; }
    public bool? Archived { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }

    [RegularExpression("^(asc|desc)$", ErrorMessage = "A ordenacao deve ser 'asc' ou 'desc'.")]
    public string OrderDir { get; set; } = "desc";
}
