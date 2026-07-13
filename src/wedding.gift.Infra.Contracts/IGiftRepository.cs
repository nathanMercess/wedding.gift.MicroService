using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IGiftRepository
{
    IQueryable<Gift> QueryWithContributions();
    Task<IReadOnlyList<Gift>> GetAllWithContributionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Gift>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, Guid? coupleId, CancellationToken cancellationToken);
    Task<Gift?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Gift?> GetByIdWithContributionsAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Gift gift, CancellationToken cancellationToken);
    void Delete(Gift gift);
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task<int> CountFullyFundedAsync(CancellationToken cancellationToken);
    Task<decimal> SumTotalAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
