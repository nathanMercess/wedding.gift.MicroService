namespace wedding.gift.Services.Contracts;

public interface IWebhookInboxService
{
    Task<bool> TryBeginAsync(
        string eventKey,
        string eventType,
        string resourceId,
        string? correlationId,
        CancellationToken cancellationToken);
    Task MarkProcessedAsync(string eventKey, CancellationToken cancellationToken);
    Task MarkFailedAsync(string eventKey, string error, CancellationToken cancellationToken);
}
