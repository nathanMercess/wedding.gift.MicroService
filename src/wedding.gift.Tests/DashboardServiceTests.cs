using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using Xunit;

namespace wedding.gift.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetAsync_DeveRetornarResumoCompleto_QuandoExistemDados()
    {
        AppDbContext context = CreateContext();
        DateTime now = DateTime.UtcNow;
        Gift completedGift = SeedGift(context, "Jogo de Panelas", "Cozinha", 100m);
        Gift partialGift = SeedGift(context, "Cafeteira", "Cozinha", 300m);

        context.Contributions.AddRange(
            Contribution.Create(completedGift.Id, "Ana", "Felicidades!", 100m, "pix", now.AddDays(-1), ContributionStatus.Paid),
            Contribution.Create(partialGift.Id, "Bruno", string.Empty, 50m, "credit_card", now, ContributionStatus.Paid),
            Contribution.Create(partialGift.Id, "Carla", string.Empty, 25m, "pix", now, ContributionStatus.Pending));

        Payment approvedPayment = Payment.CreatePix(completedGift.Id, "Ana", "Felicidades!", "ana@test.com", "CPF", "12345678909", "ord_approved", 100m, "approved", null, "mp_approved", null, string.Empty, null);
        approvedPayment.MarkContributionCreated(Guid.NewGuid());

        context.Payments.AddRange(
            approvedPayment,
            Payment.CreatePix(partialGift.Id, "Duda", "Vou pagar no Pix", "duda@test.com", "CPF", "12345678909", "ord_pending", 75m, "pending", null, "mp_pending", null, string.Empty, null),
            Payment.CreateCard(partialGift.Id, "Eva", string.Empty, "eva@test.com", "CPF", "12345678909", null, "ord_rejected", "credit_card", 80m, 1, "rejected", "cc_rejected", "mp_rejected", null));

        context.ApiRequestLogs.AddRange(
            ApiRequestLog.Create(now.AddMinutes(-3), now.AddMinutes(-3).AddMilliseconds(120), 120, "GET", "/api/gifts", string.Empty, string.Empty, 200, false, string.Empty, string.Empty, string.Empty, string.Empty, "req_200", string.Empty, string.Empty),
            ApiRequestLog.Create(now.AddMinutes(-2), now.AddMinutes(-2).AddMilliseconds(250), 250, "GET", "/api/admin/dashboard", string.Empty, string.Empty, 403, true, string.Empty, UserRoles.Admin, string.Empty, string.Empty, "req_403", string.Empty, string.Empty),
            ApiRequestLog.Create(now.AddMinutes(-1), now.AddMinutes(-1).AddMilliseconds(1250), 1250, "POST", "/api/payment/pix", string.Empty, string.Empty, 500, false, string.Empty, string.Empty, string.Empty, string.Empty, "req_500", "InvalidOperationException", "Falha inesperada"));

        await context.SaveChangesAsync(CancellationToken.None);
        DashboardService service = CreateService(context);

        DashboardResponseDto dashboard = await service.GetAsync(new DashboardQueryDto
        {
            Days = 7,
            RecentItems = 10
        }, CancellationToken.None);

        Assert.Equal(2, dashboard.Gifts.Total);
        Assert.Equal(1, dashboard.Gifts.FullyFunded);
        Assert.Equal(150m, dashboard.Overview.TotalRaised);
        Assert.Equal(400m, dashboard.Overview.TotalGoal);
        Assert.Equal(37.5m, dashboard.Overview.FundingPercent);
        Assert.Equal(2, dashboard.Contributions.Paid);
        Assert.Equal(1, dashboard.Contributions.Pending);
        Assert.Equal(2, dashboard.Contributions.UniqueContributors);
        Assert.Equal(150m, dashboard.Contributions.PeriodPaidAmount);
        Assert.Equal(1, dashboard.Payments.Approved);
        Assert.Equal(1, dashboard.Payments.Pending);
        Assert.Equal(1, dashboard.Payments.Failed);
        Assert.Equal(33.33m, dashboard.Payments.SuccessRate);
        Assert.Equal(33.33m, dashboard.Payments.FailureRate);
        Assert.Equal(2, dashboard.Messages.Total);
        Assert.Equal(1, dashboard.Messages.ContributionMessages);
        Assert.Equal(1, dashboard.Messages.PaymentIntentMessages);
        Assert.Equal(7, dashboard.ContributionsByDay.Count);
        Assert.Contains(dashboard.PaymentsByStatus, x => x.Status == "approved" && x.Count == 1);
        Assert.Contains(dashboard.PaymentsByStatus, x => x.Status == "pending" && x.Count == 1);
        Assert.Contains(dashboard.PaymentsByStatus, x => x.Status == "rejected" && x.Count == 1);
        Assert.Single(dashboard.RecentFailedPayments);
        Assert.Equal(3, dashboard.Requests.Total);
        Assert.Equal(1, dashboard.Requests.Successful);
        Assert.Equal(1, dashboard.Requests.ClientErrors);
        Assert.Equal(1, dashboard.Requests.ServerErrors);
        Assert.Equal(33.33m, dashboard.Requests.SuccessRate);
        Assert.Equal(540m, dashboard.Requests.AverageDurationMilliseconds);
        Assert.Equal(1250, dashboard.Requests.MaxDurationMilliseconds);
        Assert.Equal(1, dashboard.Requests.SlowRequests);
        Assert.Contains(dashboard.RequestsByStatus, x => x.StatusGroup == "2xx" && x.Count == 1);
        Assert.Contains(dashboard.RequestsByStatus, x => x.StatusGroup == "4xx" && x.Count == 1);
        Assert.Contains(dashboard.RequestsByStatus, x => x.StatusGroup == "5xx" && x.Count == 1);
        Assert.Contains(dashboard.RequestsByPath, x => x.Path == "/api/payment/pix" && x.ServerErrors == 1);
        Assert.Equal("req_500", dashboard.RecentRequests.First().CorrelationId);
        Assert.Equal("ApiRequestLogs ativo", dashboard.Monitoring.ApplicationLogsStatus);
        Assert.Equal(1, dashboard.Monitoring.ServerErrorRequests);
        Assert.Equal(1, dashboard.Monitoring.SlowRequests);
        Assert.Equal("critical", dashboard.ActionCenter.HealthStatus);
        Assert.Contains(dashboard.ActionCenter.Items, x => x.Category == "api" && x.Severity == "critical");
        Assert.Contains(dashboard.ActionCenter.Items, x => x.Category == "gifts" && x.Severity == "critical");
        Assert.Equal(150m, dashboard.Revenue.TotalRaised);
        Assert.Equal(250m, dashboard.Revenue.RemainingAmount);
        Assert.Equal(1, dashboard.PaymentHealth.FailedLast24Hours);
        Assert.Single(dashboard.PaymentHealth.TopFailureReasons);
        Assert.Equal(1, dashboard.GiftInsights.FullyFundedButAvailable);
        Assert.Equal(1, dashboard.ApiHealth.ServerErrors);
        Assert.Equal(1, dashboard.ApiHealth.SlowRequests);
        Assert.Contains(dashboard.ActivityFeed, x => x.Type == "api" && x.Severity == "critical" && x.CorrelationId == "req_500");

        DashboardActionCenterDto actionCenter = await service.GetActionCenterAsync(new DashboardQueryDto
        {
            Days = 7,
            RecentItems = 10
        }, CancellationToken.None);

        Assert.Equal(dashboard.ActionCenter.HealthStatus, actionCenter.HealthStatus);
    }

    [Fact]
    public async Task GetAsync_DeveLancarBadRequest_QuandoDaysInvalido()
    {
        AppDbContext context = CreateContext();
        DashboardService service = CreateService(context);

        BadRequestException ex = await Assert.ThrowsAsync<BadRequestException>(() => service.GetAsync(new DashboardQueryDto
        {
            Days = 0
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.INVALID_DASHBOARD_DAYS, ex.Code);
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DashboardService CreateService(AppDbContext context)
    {
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        return new(
            new GiftRepository(context),
            new ContributionRepository(context),
            new PaymentRepository(context),
            new ApiRequestLogRepository(context),
            cache,
            new ApplicationCacheService(cache),
            NullLogger<DashboardService>.Instance);
    }

    private static Gift SeedGift(AppDbContext context, string name, string category, decimal total)
    {
        Gift gift = Gift.Create(name, $"{name} description", total, total, $"{name}.jpg", category, true, true);

        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }
}
