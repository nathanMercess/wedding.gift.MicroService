namespace wedding.gift.Services.Contracts;

/// <summary>
/// Fila em processo para desacoplar trabalho de I/O (webhooks, e-mails) do request HTTP.
/// O worker (QueuedHostedService) cria um escopo de DI próprio por item.
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem);

    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
