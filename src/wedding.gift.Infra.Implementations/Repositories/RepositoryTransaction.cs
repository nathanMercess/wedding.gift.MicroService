using Microsoft.EntityFrameworkCore.Storage;
using wedding.gift.Infra.Contracts;

namespace wedding.gift.Infra.Implementations.Repositories;

internal sealed class RepositoryTransaction(IDbContextTransaction transaction) : IRepositoryTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => transaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync()
        => transaction.DisposeAsync();
}
