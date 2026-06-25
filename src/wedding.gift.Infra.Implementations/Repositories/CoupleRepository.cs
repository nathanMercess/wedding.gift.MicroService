using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class CoupleRepository(AppDbContext context) : ICoupleRepository
{
    public async Task<Couple?> GetAsync(bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<Couple> query = context.Couples;

        if (!tracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Couple couple, CancellationToken cancellationToken)
        => await context.Couples.AddAsync(couple, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);
}
