namespace wedding.gift.Domain.Model.Entities;

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage() { }
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string RecipientEmail { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string GiftName { get; private set; } = string.Empty;
    public string CoupleNames { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public string Method { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime PaymentDateUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime NextAttemptAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    public static EmailOutboxMessage Create(Payment payment, string type, string coupleNames)
        => new()
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Type = type,
            RecipientEmail = payment.PayerEmail,
            RecipientName = payment.ContributorName,
            GiftName = payment.GiftName,
            CoupleNames = coupleNames,
            OrderId = payment.OrderId,
            Method = payment.Method,
            Message = payment.Message ?? string.Empty,
            Amount = payment.Amount,
            PaymentDateUtc = payment.UpdatedAt,
            CreatedAtUtc = DateTime.UtcNow,
            NextAttemptAtUtc = DateTime.UtcNow
        };

    public void MarkSent(DateTime nowUtc) { Attempts++; Status = "Sent"; SentAtUtc = nowUtc; LastError = null; }
    public void MarkProcessing(DateTime leaseExpiresAtUtc) { Status = "Processing"; NextAttemptAtUtc = leaseExpiresAtUtc; }
    public void MarkFailed(string error, DateTime nextAttemptAtUtc)
    {
        Attempts++;
        Status = Attempts >= 10 ? "Failed" : "Pending";
        LastError = error.Length <= 500 ? error : error[..500];
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
