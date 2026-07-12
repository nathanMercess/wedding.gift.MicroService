using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IOperationalRepository
{
    IQueryable<PaymentOrderLookupToken> LookupTokens { get; }
    IQueryable<OrderLookupAttempt> LookupAttempts { get; }
    IQueryable<EmailOutboxMessage> EmailOutbox { get; }
    Task AddLookupTokenAsync(PaymentOrderLookupToken token, CancellationToken cancellationToken);
    Task AddLookupAttemptAsync(OrderLookupAttempt attempt, CancellationToken cancellationToken);
    Task AddEmailOutboxAsync(EmailOutboxMessage message, CancellationToken cancellationToken);
    Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task<EmailOutboxMessage?> TryClaimEmailOutboxAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
