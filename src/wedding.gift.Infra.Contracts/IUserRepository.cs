using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IUserRepository
{
    IQueryable<User> Query();
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByNormalizedEmailAsync(string normalizedEmail, bool tracking, CancellationToken cancellationToken);
    Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
