using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using Xunit;

namespace wedding.gift.Tests;

public sealed class PaymentSqlServerIntegrationTests
{
    [Fact]
    public async Task ProcessCardPaymentAsync_DeveExecutarTransacoesComRetryStrategyNoSqlServer()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string databaseName = $"wedding-gift-payment-tests-{Guid.NewGuid():N}";
        string connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;

        await using AppDbContext context = new(options);
        try
        {
            await context.Database.EnsureCreatedAsync();
            Gift gift = Gift.Create("Presente SQL", string.Empty, 100m, 100m, string.Empty, string.Empty, true);
            context.Gifts.Add(gift);
            await context.SaveChangesAsync();

            PaymentService service = CreateService(context);
            PaymentResponseDto result = await service.ProcessCardPaymentAsync(new CardPaymentRequestDto
            {
                GiftId = gift.Id,
                ContributorName = "Ana",
                CardToken = "token",
                OrderId = Guid.NewGuid().ToString("D"),
                Amount = 100m,
                Installments = 1,
                Method = "credit_card",
                PaymentMethodId = "visa",
                PayerEmail = "ana@example.com",
                PayerDocNumber = "12345678909"
            }, CancellationToken.None);

            context.ChangeTracker.Clear();

            Assert.Equal(PaymentStatuses.Approved, result.Status);
            Assert.True(result.ContributionCreated);
            Assert.Equal("PAY_SQL_1", result.MpPaymentId);
            Assert.Equal(1, await context.Payments.CountAsync());
            Assert.Equal(1, await context.Contributions.CountAsync());
            Assert.True((await context.Payments.SingleAsync()).ContributionCreated);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveSerializarReembolsosConcorrentesEntreContextos()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string databaseName = $"wedding-gift-refund-tests-{Guid.NewGuid():N}";
        string connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        FakeMercadoPago provider = new();
        string orderId;

        await using (AppDbContext setupContext = new(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            Gift gift = Gift.Create("Presente SQL", string.Empty, 100m, 100m, string.Empty, string.Empty, true);
            setupContext.Gifts.Add(gift);
            await setupContext.SaveChangesAsync();
            CardPaymentRequestDto request = new()
            {
                GiftId = gift.Id,
                ContributorName = "Ana",
                CardToken = "token",
                OrderId = Guid.NewGuid().ToString("D"),
                Amount = 100m,
                Installments = 1,
                Method = "credit_card",
                PaymentMethodId = "visa",
                PayerEmail = "ana@example.com",
                PayerDocNumber = "12345678909"
            };
            orderId = request.OrderId;
            await CreateService(setupContext, provider).ProcessCardPaymentAsync(request, CancellationToken.None);
        }

        try
        {
            await using AppDbContext firstContext = new(options);
            await using AppDbContext secondContext = new(options);
            PaymentService firstService = CreateService(firstContext, provider);
            PaymentService secondService = CreateService(secondContext, provider);

            Task<Exception?> first = CaptureExceptionAsync(
                () => firstService.RefundPaymentAsync(orderId, 60m, Guid.NewGuid(), CancellationToken.None));
            Task<Exception?> second = CaptureExceptionAsync(
                () => secondService.RefundPaymentAsync(orderId, 60m, Guid.NewGuid(), CancellationToken.None));
            Exception?[] exceptions = await Task.WhenAll(first, second);

            Assert.Single(exceptions, exception => exception is null);
            ConflictException conflict = Assert.IsType<ConflictException>(Assert.Single(exceptions, exception => exception is not null));
            Assert.Equal(PaymentErrorCodes.InvalidRefundAmount, conflict.Code);
            Assert.Equal(1, provider.RefundCount);

            await using AppDbContext verificationContext = new(options);
            Assert.Equal(60m, (await verificationContext.Payments.SingleAsync()).RefundedAmount);
            Assert.Equal(1, await verificationContext.PaymentRefundOperations.CountAsync());
        }
        finally
        {
            await using AppDbContext cleanupContext = new(options);
            await cleanupContext.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveSerializarMesmaChaveEntrePagamentosDiferentes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string databaseName = $"wedding-gift-refund-key-tests-{Guid.NewGuid():N}";
        string connectionString = $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Integrated Security=true;TrustServerCertificate=true";
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        string firstOrderId = Guid.NewGuid().ToString("D");
        string secondOrderId = Guid.NewGuid().ToString("D");

        await using (AppDbContext setupContext = new(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            Gift gift = Gift.Create("Presente SQL", string.Empty, 200m, 200m, string.Empty, string.Empty, true);
            setupContext.Gifts.Add(gift);
            setupContext.Payments.AddRange(
                Payment.CreateCard(gift.Id, gift.Name, "Ana", string.Empty, "ana@example.com", "CPF", "12345678909", null, firstOrderId, "credit_card", 100m, 1, PaymentStatuses.Approved, null, null, "PAY_KEY_1"),
                Payment.CreateCard(gift.Id, gift.Name, "Bia", string.Empty, "bia@example.com", "CPF", "12345678909", null, secondOrderId, "credit_card", 100m, 1, PaymentStatuses.Approved, null, null, "PAY_KEY_2"));
            await setupContext.SaveChangesAsync();
        }

        try
        {
            FakeMercadoPago firstProvider = new();
            FakeMercadoPago secondProvider = new();
            Guid operationId = Guid.NewGuid();
            await using AppDbContext firstContext = new(options);
            await using AppDbContext secondContext = new(options);
            PaymentService firstService = CreateService(firstContext, firstProvider);
            PaymentService secondService = CreateService(secondContext, secondProvider);

            Exception?[] exceptions = await Task.WhenAll(
                CaptureExceptionAsync(() => firstService.RefundPaymentAsync(firstOrderId, 35m, operationId, CancellationToken.None)),
                CaptureExceptionAsync(() => secondService.RefundPaymentAsync(secondOrderId, 35m, operationId, CancellationToken.None)));

            Assert.Single(exceptions, exception => exception is null);
            ConflictException conflict = Assert.IsType<ConflictException>(Assert.Single(exceptions, exception => exception is not null));
            Assert.Equal(PaymentErrorCodes.IdempotencyKeyAlreadyUsed, conflict.Code);
            Assert.Equal(1, firstProvider.RefundCount + secondProvider.RefundCount);

            await using AppDbContext verificationContext = new(options);
            Assert.Equal(1, await verificationContext.PaymentRefundOperations.CountAsync());
            Assert.Equal(35m, await verificationContext.Payments.SumAsync(payment => payment.RefundedAmount));
        }
        finally
        {
            await using AppDbContext cleanupContext = new(options);
            await cleanupContext.Database.EnsureDeletedAsync();
        }
    }

    private static PaymentService CreateService(AppDbContext context, IMercadoPagoService? provider = null)
    {
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());

        return new PaymentService(
            provider ?? new FakeMercadoPago(),
            new PaymentRepository(context),
            new GiftRepository(context),
            new ContributionRepository(context),
            new CoupleRepository(context),
            new FakeEmail(),
            new ApplicationCacheService(cache),
            NullLogger<PaymentService>.Instance);
    }

    private static async Task<Exception?> CaptureExceptionAsync(Func<Task<PaymentResponseDto>> operation)
    {
        try
        {
            await operation();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private sealed class FakeMercadoPago : IMercadoPagoService
    {
        private readonly object _sync = new();
        private decimal _refundedAmount;
        private int _refundCount;

        public int RefundCount => _refundCount;

        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_SQL_1", Amount = request.Amount, CurrencyId = "BRL", Method = request.Method });

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Pending, MpOrderId = "ORD_SQL_1" });

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                return Task.FromResult(new PaymentResponseDto
                {
                    Status = _refundedAmount > 0 ? PaymentStatuses.PartiallyRefunded : PaymentStatuses.Approved,
                    MpPaymentId = mpOrderId,
                    Amount = 100m,
                    RefundedAmount = _refundedAmount,
                    CurrencyId = "BRL",
                    Method = "credit_card"
                });
            }
        }

        public Task<PaymentResponseDto> GetChargebackAsync(string chargebackId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, string idempotencyKey, CancellationToken cancellationToken)
            => RefundAsync(mpOrderId, mpPaymentId, null, idempotencyKey, cancellationToken);

        public async Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _refundCount);
            await Task.Delay(250, cancellationToken);

            lock (_sync)
            {
                _refundedAmount += amount ?? 100m;
                return new PaymentResponseDto
                {
                    Status = _refundedAmount >= 100m ? PaymentStatuses.Refunded : PaymentStatuses.PartiallyRefunded,
                    MpPaymentId = mpPaymentId,
                    RefundedAmount = _refundedAmount
                };
            }
        }
    }

    private sealed class FakeEmail : IEmailService
    {
        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
