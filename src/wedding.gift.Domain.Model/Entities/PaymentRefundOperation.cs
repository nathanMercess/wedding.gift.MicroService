namespace wedding.gift.Domain.Model.Entities;

public sealed class PaymentRefundOperation
{
    private PaymentRefundOperation()
    {
    }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Payment Payment { get; private set; } = null!;
    public Guid IdempotencyKey { get; private set; }
    public decimal Amount { get; private set; }
    public bool IsFullRefund { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static PaymentRefundOperation Create(
        Guid paymentId,
        Guid idempotencyKey,
        decimal amount,
        bool isFullRefund,
        decimal refundedAmount)
        => new()
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            IdempotencyKey = idempotencyKey,
            Amount = amount,
            IsFullRefund = isFullRefund,
            RefundedAmount = refundedAmount,
            CreatedAt = DateTime.UtcNow
        };
}
