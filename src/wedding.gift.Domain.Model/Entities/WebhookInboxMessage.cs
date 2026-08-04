namespace wedding.gift.Domain.Model.Entities;

public sealed class WebhookInboxMessage
{
    private WebhookInboxMessage()
    {
    }

    public Guid Id { get; private init; }
    public string Provider { get; private init; } = string.Empty;
    public string EventKey { get; private init; } = string.Empty;
    public string EventType { get; private init; } = string.Empty;
    public string ResourceId { get; private init; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public int Attempts { get; private set; }
    public string? CorrelationId { get; private init; }
    public string? LastError { get; private set; }
    public DateTime ReceivedAtUtc { get; private init; }
    public DateTime UpdatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }

    public static WebhookInboxMessage Create(
        string provider,
        string eventKey,
        string eventType,
        string resourceId,
        string? correlationId)
    {
        DateTime now = DateTime.UtcNow;
        return new WebhookInboxMessage
        {
            Id = Guid.NewGuid(),
            Provider = provider.Trim(),
            EventKey = eventKey.Trim(),
            EventType = eventType.Trim(),
            ResourceId = resourceId.Trim(),
            Status = "Processing",
            Attempts = 1,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
            ReceivedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Retry()
    {
        Status = "Processing";
        Attempts++;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkProcessed()
    {
        Status = "Processed";
        LastError = null;
        ProcessedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = ProcessedAtUtc.Value;
    }

    public void MarkFailed(string error)
    {
        Status = "Failed";
        LastError = string.IsNullOrWhiteSpace(error) ? null : error.Trim()[..Math.Min(error.Trim().Length, 500)];
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
