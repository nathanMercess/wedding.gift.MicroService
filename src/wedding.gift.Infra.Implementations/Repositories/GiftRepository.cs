using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class GiftRepository(AppDbContext context) : IGiftRepository
{
    public IQueryable<Gift> QueryWithContributions()
        => context.Gifts.Include(x => x.Contributions).AsNoTracking();

    public async Task<IReadOnlyList<Gift>> GetAllWithContributionsAsync(CancellationToken cancellationToken)
        => await context.Gifts
            .Include(x => x.Contributions)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<Gift?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Gift?> GetByIdWithContributionsAsync(Guid id, CancellationToken cancellationToken)
        => await context.Gifts
            .Include(x => x.Contributions)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
        => await context.Gifts.AnyAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(Gift gift, CancellationToken cancellationToken)
        => await context.Gifts.AddAsync(gift, cancellationToken);

    public void Delete(Gift gift)
        => context.Gifts.Remove(gift);

    public async Task<int> CountAsync(CancellationToken cancellationToken)
        => await context.Gifts.CountAsync(cancellationToken);

    public async Task<int> CountFullyFundedAsync(CancellationToken cancellationToken)
        => await context.Gifts
            .GroupJoin(
                context.Contributions.Where(c => c.Status == ContributionStatus.Paid),
                gift => gift.Id,
                contribution => contribution.GiftId,
                (gift, contributions) => new
                {
                    gift.Total,
                    Raised = contributions.Sum(c => c.Amount)
                })
            .CountAsync(g => g.Raised >= g.Total, cancellationToken);

    public async Task<decimal> SumTotalAsync(CancellationToken cancellationToken)
        => await context.Gifts.SumAsync(x => x.Total, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);
}
