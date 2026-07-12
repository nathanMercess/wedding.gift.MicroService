namespace wedding.gift.Domain.Model.Entities;

public sealed class PaymentOrderLookupToken
{
    private PaymentOrderLookupToken() { }

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public static PaymentOrderLookupToken Create(Guid paymentId, string tokenHash, DateTime expiresAtUtc)
        => new()
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            TokenHash = tokenHash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc
        };

    public bool IsValid(DateTime nowUtc)
        => ConsumedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Consume(DateTime nowUtc) => ConsumedAtUtc = nowUtc;
    public void Revoke(DateTime nowUtc) => RevokedAtUtc = nowUtc;
}
