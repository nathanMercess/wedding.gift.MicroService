using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Implementations;
using Xunit;

namespace wedding.gift.Tests;

public sealed class WebhookInboxServiceTests
{
    [Fact]
    public async Task TryBeginAsync_DeveDeduplicarEventoEmProcessamentoEProcessado()
    {
        await using AppDbContext context = CreateContext();
        WebhookInboxService service = new(new OperationalRepository(context));

        bool first = await service.TryBeginAsync("event-1", "payment", "resource-1", "correlation-1", CancellationToken.None);
        bool concurrentDuplicate = await service.TryBeginAsync("event-1", "payment", "resource-1", "correlation-2", CancellationToken.None);
        await service.MarkProcessedAsync("event-1", CancellationToken.None);
        bool processedDuplicate = await service.TryBeginAsync("event-1", "payment", "resource-1", "correlation-3", CancellationToken.None);

        Assert.True(first);
        Assert.False(concurrentDuplicate);
        Assert.False(processedDuplicate);
        WebhookInboxMessage message = Assert.Single(context.WebhookInboxMessages);
        Assert.Equal("Processed", message.Status);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public async Task TryBeginAsync_DevePermitirRetryDepoisDeFalha()
    {
        await using AppDbContext context = CreateContext();
        WebhookInboxService service = new(new OperationalRepository(context));
        await service.TryBeginAsync("event-2", "payment", "resource-2", null, CancellationToken.None);
        await service.MarkFailedAsync("event-2", "timeout", CancellationToken.None);

        bool retry = await service.TryBeginAsync("event-2", "payment", "resource-2", null, CancellationToken.None);

        Assert.True(retry);
        WebhookInboxMessage message = Assert.Single(context.WebhookInboxMessages);
        Assert.Equal("Processing", message.Status);
        Assert.Equal(2, message.Attempts);
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
