using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IApiRequestLogRepository
{
    Task AddAsync(ApiRequestLog requestLog, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiRequestLog>> GetByStartedAtRangeAsync(DateTime fromUtc, DateTime toUtc, int maxItems, CancellationToken cancellationToken);
    Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
