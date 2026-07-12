using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class ApiRequestLogCleanupHostedService(
    IServiceProvider services,
    ILogger<ApiRequestLogCleanupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CleanupAsync(stoppingToken);
        using PeriodicTimer timer = new(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await CleanupAsync(stoppingToken);
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = services.CreateScope();
            IApiRequestLogService service = scope.ServiceProvider.GetRequiredService<IApiRequestLogService>();
            int deleted = await service.CleanupAsync(DateTime.UtcNow.Subtract(Retention), cancellationToken);

            if (deleted > 0)
                logger.LogInformation("Logs de request expirados removidos. Count={Count}.", deleted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao remover logs de request expirados.");
        }
    }
}
