using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class EmailOutboxHostedService(IServiceProvider services, ILogger<EmailOutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));
        do
        {
            await ProcessBatchAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = services.CreateScope();
        IOperationalRepository repository = scope.ServiceProvider.GetRequiredService<IOperationalRepository>();
        IOrderLookupService lookupService = scope.ServiceProvider.GetRequiredService<IOrderLookupService>();
        IEmailService emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        DateTime nowUtc = DateTime.UtcNow;
        List<Guid> messageIds = await repository.EmailOutbox
            .Where(x => (x.Status == "Pending" || x.Status == "Processing") && x.NextAttemptAtUtc <= nowUtc)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(20)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid messageId in messageIds)
        {
            EmailOutboxMessage? message = await repository.TryClaimEmailOutboxAsync(messageId, DateTime.UtcNow, cancellationToken);
            if (message is null)
                continue;

            try
            {
                string token = await lookupService.CreateTokenAsync(message.PaymentId, cancellationToken);
                await emailService.SendGuestReceiptAsync(
                    message.RecipientEmail, message.RecipientName, message.CoupleNames, message.GiftName,
                    message.OrderId, message.Amount, message.Method, message.PaymentDateUtc, message.Message,
                    token, cancellationToken);
                message.MarkSent(DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar e-mail persistente {OutboxId}.", message.Id);
                message.MarkFailed(ex.GetType().Name, DateTime.UtcNow.AddMinutes(Math.Min(Math.Pow(2, message.Attempts + 1), 60)));
            }

            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
