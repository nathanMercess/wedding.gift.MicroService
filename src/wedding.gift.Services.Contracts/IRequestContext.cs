namespace wedding.gift.Services.Contracts;

public interface IRequestContext
{
    Guid? UserId { get; }
    Guid? CoupleId { get; }
    bool IsSuperAdmin { get; }
    string CorrelationId { get; }
    string RemoteIpAddress { get; }
}
