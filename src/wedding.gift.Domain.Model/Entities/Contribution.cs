using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Domain.Model.Entities;

public sealed class Contribution
{
    private Contribution()
    {
    }

    public Guid Id { get; private set; }
    public Guid GiftId { get; private set; }
    public string ContributorName { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public DateTime PaidAt { get; private set; }
    public string Status { get; private set; } = ContributionStatus.Pending;
    public Gift Gift { get; private set; } = null!;

    public static Contribution Create(
        Guid giftId,
        string contributorName,
        string message,
        decimal amount,
        string paymentMethod,
        DateTime paidAt,
        string status)
    {
        Contribution contribution = new()
        {
            Id = Guid.NewGuid(),
            GiftId = giftId,
            ContributorName = contributorName.Trim(),
            Message = message?.Trim() ?? string.Empty,
            Amount = amount,
            PaymentMethod = paymentMethod?.Trim() ?? string.Empty,
            PaidAt = paidAt == default ? DateTime.UtcNow : paidAt,
            Status = string.IsNullOrWhiteSpace(status) ? ContributionStatus.Pending : status.Trim()
        };

        return contribution;
    }

    public void UpdateStatus(string status, DateTime paidAt)
    {
        string previousStatus = Status;
        Status = status.Trim();

        if (Status == ContributionStatus.Paid && previousStatus != ContributionStatus.Paid)
            PaidAt = paidAt == default ? DateTime.UtcNow : paidAt;
    }
}
