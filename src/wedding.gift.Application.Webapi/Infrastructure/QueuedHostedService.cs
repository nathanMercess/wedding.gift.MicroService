using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

/// <summary>
/// Consome a fila de background. Cada item roda em seu próprio escopo de DI.
/// Falhas são SEMPRE logadas (dead-letter explícito) — nunca engolidas em silêncio.
/// </summary>
public sealed class QueuedHostedService(
    IBackgroundTaskQueue queue,
    IServiceProvider services,
    ILogger<QueuedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task> workItem;

            try
            {
                workItem = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = services.CreateScope();
                await workItem(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha definitiva ao processar item de background (dead-letter). Item descartado.");
            }
        }
    }
}
