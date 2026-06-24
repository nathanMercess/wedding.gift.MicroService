using Microsoft.EntityFrameworkCore;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Tests;

public class DashboardServiceTests
{
    [Fact]
    public async Task GetAsync_DeveRetornarResumoCompleto_QuandoExistemDados()
    {
        var context = CreateContext();
        var now = DateTime.UtcNow;
        var completedGift = SeedGift(context, "Jogo de Panelas", "Cozinha", 100m);
        var partialGift = SeedGift(context, "Cafeteira", "Cozinha", 300m);

        context.Contributions.AddRange(
            new Contribution
            {
                Id = Guid.NewGuid(),
                GiftId = completedGift.Id,
                ContributorName = "Ana",
                Message = "Felicidades!",
                Amount = 100m,
                PaymentMethod = "pix",
                PaidAt = now.AddDays(-1),
                Status = ContributionStatus.Paid
            },
            new Contribution
            {
                Id = Guid.NewGuid(),
                GiftId = partialGift.Id,
                ContributorName = "Bruno",
                Amount = 50m,
                PaymentMethod = "credit_card",
                PaidAt = now,
                Status = ContributionStatus.Paid
            },
            new Contribution
            {
                Id = Guid.NewGuid(),
                GiftId = partialGift.Id,
                ContributorName = "Carla",
                Amount = 25m,
                PaymentMethod = "pix",
                PaidAt = now,
                Status = ContributionStatus.Pending
            });

        context.Payments.AddRange(
            new Payment
            {
                Id = Guid.NewGuid(),
                GiftId = completedGift.Id,
                ContributorName = "Ana",
                Message = "Felicidades!",
                PayerEmail = "ana@test.com",
                PayerDocType = "CPF",
                PayerDocNumber = "12345678909",
                ContributionCreated = true,
                OrderId = "ord_approved",
                Method = "pix",
                Amount = 100m,
                Status = "approved",
                MpOrderId = "mp_approved",
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new Payment
            {
                Id = Guid.NewGuid(),
                GiftId = partialGift.Id,
                ContributorName = "Duda",
                Message = "Vou pagar no Pix",
                PayerEmail = "duda@test.com",
                PayerDocType = "CPF",
                PayerDocNumber = "12345678909",
                ContributionCreated = false,
                OrderId = "ord_pending",
                Method = "pix",
                Amount = 75m,
                Status = "pending",
                MpOrderId = "mp_pending",
                CreatedAt = now,
                UpdatedAt = now
            },
            new Payment
            {
                Id = Guid.NewGuid(),
                GiftId = partialGift.Id,
                ContributorName = "Eva",
                PayerEmail = "eva@test.com",
                PayerDocType = "CPF",
                PayerDocNumber = "12345678909",
                ContributionCreated = false,
                OrderId = "ord_rejected",
                Method = "credit_card",
                Amount = 80m,
                Status = "rejected",
                StatusDetail = "cc_rejected",
                MpOrderId = "mp_rejected",
                CreatedAt = now,
                UpdatedAt = now
            });

        context.ApiRequestLogs.AddRange(
            new ApiRequestLog
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = now.AddMinutes(-3),
                CompletedAtUtc = now.AddMinutes(-3).AddMilliseconds(120),
                DurationMilliseconds = 120,
                Method = "GET",
                Path = "/api/gifts",
                StatusCode = 200,
                IsSuccess = true,
                IsAuthenticated = false,
                CorrelationId = "req_200"
            },
            new ApiRequestLog
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = now.AddMinutes(-2),
                CompletedAtUtc = now.AddMinutes(-2).AddMilliseconds(250),
                DurationMilliseconds = 250,
                Method = "GET",
                Path = "/api/admin/dashboard",
                StatusCode = 403,
                IsSuccess = false,
                IsAuthenticated = true,
                UserRole = UserRoles.Admin,
                CorrelationId = "req_403"
            },
            new ApiRequestLog
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = now.AddMinutes(-1),
                CompletedAtUtc = now.AddMinutes(-1).AddMilliseconds(1250),
                DurationMilliseconds = 1250,
                Method = "POST",
                Path = "/api/payment/pix",
                StatusCode = 500,
                IsSuccess = false,
                IsAuthenticated = false,
                CorrelationId = "req_500",
                ExceptionType = "InvalidOperationException",
                ExceptionMessage = "Falha inesperada"
            });

        await context.SaveChangesAsync();
        var service = new DashboardService(context);

        var dashboard = await service.GetAsync(new DashboardQueryDto
        {
            Days = 7,
            RecentItems = 5
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
    }

    [Fact]
    public async Task GetAsync_DeveLancarBadRequest_QuandoDaysInvalido()
    {
        var context = CreateContext();
        var service = new DashboardService(context);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.GetAsync(new DashboardQueryDto
        {
            Days = 0
        }, CancellationToken.None));

        Assert.Equal("O parametro 'days' deve estar entre 1 e 365.", ex.Message);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Gift SeedGift(AppDbContext context, string name, string category, decimal total)
    {
        var gift = new Gift
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description",
            Price = total,
            Total = total,
            Image = $"{name}.jpg",
            Category = category,
            Available = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }
}
