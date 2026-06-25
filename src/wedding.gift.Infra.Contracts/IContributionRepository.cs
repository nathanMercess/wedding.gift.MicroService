using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IContributionRepository
{
    IQueryable<Contribution> Query();
    Task<IReadOnlyList<Contribution>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Contribution>> GetByGiftIdAsync(Guid giftId, CancellationToken cancellationToken);
    Task<Contribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Contribution contribution, CancellationToken cancellationToken);
    Task<decimal> SumPaidAmountAsync(CancellationToken cancellationToken);
    Task<int> CountUniquePaidContributorsAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
