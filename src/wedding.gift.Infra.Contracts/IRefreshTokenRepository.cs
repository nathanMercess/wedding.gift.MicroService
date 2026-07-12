using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
