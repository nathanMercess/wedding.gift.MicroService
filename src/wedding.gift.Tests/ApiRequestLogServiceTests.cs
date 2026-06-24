using Microsoft.EntityFrameworkCore;
using Xunit;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Implementations;

namespace wedding.gift.Tests;

public class ApiRequestLogServiceTests
{
    [Fact]
    public async Task SaveAsync_DevePersistirMetadadosSegurosDoRequest()
    {
        var context = CreateContext();
        var service = new ApiRequestLogService(context);

        await service.SaveAsync(new ApiRequestLogCreateDto
        {
            StartedAtUtc = DateTime.UtcNow.AddMilliseconds(-42),
            CompletedAtUtc = DateTime.UtcNow,
            DurationMilliseconds = 42,
            Method = "GET",
            Path = "/api/admin/dashboard",
            QueryString = "?days=30&token=[redacted]",
            EndpointName = "wedding.gift.Application.Webapi.Controllers.AdminDashboardController.Get",
            StatusCode = 200,
            IsAuthenticated = true,
            UserId = Guid.NewGuid().ToString(),
            UserRole = "SuperAdmin",
            ClientIp = "127.0.0.1",
            UserAgent = "Tests",
            CorrelationId = "trace-1"
        }, CancellationToken.None);

        var log = await context.ApiRequestLogs.SingleAsync();

        Assert.Equal("/api/admin/dashboard", log.Path);
        Assert.Equal("?days=30&token=[redacted]", log.QueryString);
        Assert.Equal(200, log.StatusCode);
        Assert.True(log.IsSuccess);
        Assert.True(log.IsAuthenticated);
        Assert.Equal("SuperAdmin", log.UserRole);
        Assert.Equal("trace-1", log.CorrelationId);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
