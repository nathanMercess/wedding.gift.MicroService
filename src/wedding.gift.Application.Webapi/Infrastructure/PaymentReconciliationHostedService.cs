using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class PaymentReconciliationHostedService(
    IServiceProvider services,
    ILogger<PaymentReconciliationHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using IServiceScope scope = services.CreateScope();
                IPaymentService service = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                await service.ReconcilePendingPaymentsAsync(stoppingToken);
                await service.ReconcileApprovedPaymentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha na reconciliação periódica de pagamentos.");
            }
        }
    }
}
