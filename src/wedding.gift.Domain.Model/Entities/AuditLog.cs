namespace wedding.gift.Domain.Model.Entities;

public sealed class AuditLog
{
    private AuditLog() { }
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CoupleId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public static AuditLog Create(Guid? userId, Guid? coupleId, string action, string entityType, string entityId, string correlationId)
        => new() { Id = Guid.NewGuid(), UserId = userId, CoupleId = coupleId, Action = action, EntityType = entityType, EntityId = entityId, CorrelationId = correlationId, CreatedAtUtc = DateTime.UtcNow };
}
