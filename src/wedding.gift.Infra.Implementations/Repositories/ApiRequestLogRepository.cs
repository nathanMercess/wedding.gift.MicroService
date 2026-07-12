using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class ApiRequestLogRepository(AppDbContext context) : IApiRequestLogRepository
{
    public async Task AddAsync(ApiRequestLog requestLog, CancellationToken cancellationToken)
        => await context.ApiRequestLogs.AddAsync(requestLog, cancellationToken);

    public async Task<IReadOnlyList<ApiRequestLog>> GetByStartedAtRangeAsync(DateTime fromUtc, DateTime toUtc, int maxItems, CancellationToken cancellationToken)
        => await context.ApiRequestLogs
            .AsNoTracking()
            .Where(x => x.StartedAtUtc >= fromUtc && x.StartedAtUtc <= toUtc)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(maxItems)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        var query = context.ApiRequestLogs.Where(x => x.StartedAtUtc < cutoffUtc);
        if (context.Database.IsRelational())
            return await query.ExecuteDeleteAsync(cancellationToken);

        var expiredLogs = await query.ToListAsync(cancellationToken);
        context.ApiRequestLogs.RemoveRange(expiredLogs);
        await context.SaveChangesAsync(cancellationToken);
        return expiredLogs.Count;
    }
}
