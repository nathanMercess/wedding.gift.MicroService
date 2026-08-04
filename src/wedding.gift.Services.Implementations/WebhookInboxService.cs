using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class WebhookInboxService(IOperationalRepository repository) : IWebhookInboxService
{
    public async Task<bool> TryBeginAsync(
        string eventKey,
        string eventType,
        string resourceId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        WebhookInboxMessage? existing = await repository.WebhookInbox
            .FirstOrDefaultAsync(x => x.EventKey == eventKey, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == "Processed" ||
                existing.Status == "Processing" && existing.UpdatedAtUtc > DateTime.UtcNow.AddMinutes(-5))
                return false;

            existing.Retry();
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }

        await repository.AddWebhookInboxAsync(
            WebhookInboxMessage.Create("MercadoPago", eventKey, eventType, resourceId, correlationId),
            cancellationToken);

        try
        {
            await repository.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return false;
        }
    }

    public async Task MarkProcessedAsync(string eventKey, CancellationToken cancellationToken)
    {
        WebhookInboxMessage? message = await repository.WebhookInbox
            .FirstOrDefaultAsync(x => x.EventKey == eventKey, cancellationToken);

        if (message is null)
            return;

        message.MarkProcessed();
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(string eventKey, string error, CancellationToken cancellationToken)
    {
        WebhookInboxMessage? message = await repository.WebhookInbox
            .FirstOrDefaultAsync(x => x.EventKey == eventKey, cancellationToken);

        if (message is null)
            return;

        message.MarkFailed(error);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        string message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("unique", StringComparison.OrdinalIgnoreCase);
    }
}
