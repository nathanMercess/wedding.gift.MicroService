using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class OperationalRepository(AppDbContext context) : IOperationalRepository
{
    public IQueryable<PaymentOrderLookupToken> LookupTokens => context.PaymentOrderLookupTokens;
    public IQueryable<OrderLookupAttempt> LookupAttempts => context.OrderLookupAttempts;
    public IQueryable<EmailOutboxMessage> EmailOutbox => context.EmailOutboxMessages;
    public async Task AddLookupTokenAsync(PaymentOrderLookupToken token, CancellationToken cancellationToken) => await context.PaymentOrderLookupTokens.AddAsync(token, cancellationToken);
    public async Task AddLookupAttemptAsync(OrderLookupAttempt attempt, CancellationToken cancellationToken) => await context.OrderLookupAttempts.AddAsync(attempt, cancellationToken);
    public async Task AddEmailOutboxAsync(EmailOutboxMessage message, CancellationToken cancellationToken) => await context.EmailOutboxMessages.AddAsync(message, cancellationToken);
    public async Task AddAuditLogAsync(AuditLog auditLog, CancellationToken cancellationToken) => await context.AuditLogs.AddAsync(auditLog, cancellationToken);
    public async Task<EmailOutboxMessage?> TryClaimEmailOutboxAsync(Guid id, DateTime nowUtc, CancellationToken cancellationToken)
    {
        IQueryable<EmailOutboxMessage> claimable = context.EmailOutboxMessages.Where(x =>
            x.Id == id && (x.Status == "Pending" || x.Status == "Processing") && x.NextAttemptAtUtc <= nowUtc);
        DateTime leaseExpiresAtUtc = nowUtc.AddMinutes(5);

        if (context.Database.IsRelational())
        {
            int affected = await claimable.ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, "Processing")
                .SetProperty(x => x.NextAttemptAtUtc, leaseExpiresAtUtc), cancellationToken);
            return affected == 0 ? null : await context.EmailOutboxMessages.FirstAsync(x => x.Id == id, cancellationToken);
        }

        EmailOutboxMessage? message = await claimable.FirstOrDefaultAsync(cancellationToken);
        if (message is null)
            return null;
        message.MarkProcessing(leaseExpiresAtUtc);
        await context.SaveChangesAsync(cancellationToken);
        return message;
    }
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await context.SaveChangesAsync(cancellationToken);
}
