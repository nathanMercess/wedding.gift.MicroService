using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Domain.Model.Entities;

public sealed class Contribution
{
    private Contribution()
    {
    }

    public Guid Id { get; private set; }
    public Guid CoupleId { get; private set; }
    public Guid GiftId { get; private set; }
    public string OrderId { get; private set; } = string.Empty;
    public string GuestEmail { get; private set; } = string.Empty;
    public string ContributorName { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public string PaymentStatus { get; private set; } = string.Empty;
    public DateTime PaidAt { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? MessageReadAtUtc { get; private set; }
    public DateTime? MessageArchivedAtUtc { get; private set; }
    public string Status { get; private set; } = ContributionStatus.Pending;
    public Gift Gift { get; private set; } = null!;
    public decimal NetAmount => Math.Max(Amount - RefundedAmount, 0);

    public static Contribution Create(
        Guid giftId,
        string contributorName,
        string message,
        decimal amount,
        string paymentMethod,
        DateTime paidAt,
        string status,
        Guid? coupleId = null,
        string? orderId = null,
        string? guestEmail = null,
        DateTime? createdAtUtc = null,
        string? paymentStatus = null)
    {
        Contribution contribution = new()
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId ?? Couple.SingletonId,
            GiftId = giftId,
            OrderId = orderId?.Trim() ?? string.Empty,
            GuestEmail = guestEmail?.Trim() ?? string.Empty,
            ContributorName = contributorName.Trim(),
            Message = message?.Trim() ?? string.Empty,
            Amount = amount,
            PaymentMethod = paymentMethod?.Trim() ?? string.Empty,
            PaymentStatus = paymentStatus?.Trim() ?? string.Empty,
            PaidAt = paidAt == default ? DateTime.UtcNow : paidAt,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
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

    public void ApplyRefund(decimal refundedAmount)
    {
        RefundedAmount = Math.Clamp(refundedAmount, 0, Amount);
    }

    public void UpdatePaymentStatus(string status)
        => PaymentStatus = status?.Trim() ?? string.Empty;

    public void SetMessageRead(bool read)
    {
        if (read && MessageReadAtUtc is null)
            MessageReadAtUtc = DateTime.UtcNow;
        else if (!read)
            MessageReadAtUtc = null;
    }

    public void SetMessageArchived(bool archived)
    {
        if (archived && MessageArchivedAtUtc is null)
            MessageArchivedAtUtc = DateTime.UtcNow;
        else if (!archived)
            MessageArchivedAtUtc = null;
    }
}
