using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface ICoupleRepository
{
    Task<Couple?> GetAsync(bool tracking, CancellationToken cancellationToken);
    Task<Couple?> GetByIdAsync(Guid id, bool tracking, CancellationToken cancellationToken);
    Task AddAsync(Couple couple, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
