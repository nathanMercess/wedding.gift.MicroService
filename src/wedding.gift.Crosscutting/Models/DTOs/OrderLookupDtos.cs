using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class OrderLookupRequestDto
{
    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string OrderId { get; set; } = string.Empty;
}

public sealed class OrderLookupAcceptedDto
{
    public bool Accepted { get; set; } = true;
}

public sealed class OrderLookupResponseDto
{
    public string OrderId { get; set; } = string.Empty;
    public string GiftName { get; set; } = string.Empty;
    public string GiftImage { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public bool ContributionCreated { get; set; }
}
