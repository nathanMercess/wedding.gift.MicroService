namespace wedding.gift.Infra.Contracts;

public interface IRepositoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
