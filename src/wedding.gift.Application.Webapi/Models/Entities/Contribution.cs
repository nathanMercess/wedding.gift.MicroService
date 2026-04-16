namespace wedding.gift.Application.Webapi.Models.Entities;

public class Contribution
{
    public Guid Id { get; set; }

    public Guid GiftId { get; set; }

    public string ContributorName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public DateTime PaidAt { get; set; }

    public string Status { get; set; } = ContributionStatus.Pending;

    public Gift Gift { get; set; } = null!;
}
