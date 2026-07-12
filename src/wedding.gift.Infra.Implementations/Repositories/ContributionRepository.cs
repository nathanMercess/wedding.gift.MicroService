using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class ContributionRepository(AppDbContext context) : IContributionRepository
{
    public IQueryable<Contribution> Query()
        => context.Contributions.AsNoTracking();

    public async Task<IReadOnlyList<Contribution>> GetAllAsync(CancellationToken cancellationToken)
        => await context.Contributions
            .AsNoTracking()
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Contribution>> GetByGiftIdAsync(Guid giftId, CancellationToken cancellationToken)
        => await context.Contributions
            .AsNoTracking()
            .Where(x => x.GiftId == giftId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

    public async Task<Contribution?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.Contributions.Include(x => x.Gift).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Contribution contribution, CancellationToken cancellationToken)
        => await context.Contributions.AddAsync(contribution, cancellationToken);

    public async Task<decimal> SumPaidAmountAsync(CancellationToken cancellationToken)
        => await context.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .SumAsync(x => x.Amount - x.RefundedAmount, cancellationToken);

    public async Task<int> CountUniquePaidContributorsAsync(CancellationToken cancellationToken)
        => await context.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .Select(x => x.ContributorName.Trim().ToLower())
            .Distinct()
            .CountAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);
}
