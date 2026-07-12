namespace wedding.gift.Domain.Model.Entities;

public sealed class OrderLookupAttempt
{
    private OrderLookupAttempt() { }
    public Guid Id { get; private set; }
    public string IpHash { get; private set; } = string.Empty;
    public string EmailHash { get; private set; } = string.Empty;
    public bool Matched { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static OrderLookupAttempt Create(string ipHash, string emailHash, bool matched)
        => new() { Id = Guid.NewGuid(), IpHash = ipHash, EmailHash = emailHash, Matched = matched, CreatedAtUtc = DateTime.UtcNow };
}
