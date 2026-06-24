using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;

namespace wedding.gift.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task ProcessCardPaymentAsync_DeveCriarContribuicaoPaga_QuandoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        var repo = new FakePaymentRepository();
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var queue = new FakeQueue();
        var service = CreateService(context, mp, repo, queue: queue);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 500m), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        var contribution = Assert.Single(context.Contributions);
        Assert.Equal(ContributionStatus.Paid, contribution.Status);
        Assert.Single(repo.Saved);
        Assert.Equal("approved", repo.Saved[0].Status);
        Assert.NotNull(repo.Saved[0].ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(2, queue.Items.Count);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_NaoDeveCriarContribuicao_QuandoRecusado()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var repo = new FakePaymentRepository();
        var mp = new FakeMercadoPago
        {
            CardResult = new PaymentResponseDto
            {
                Status = "rejected",
                StatusDetail = "cc_rejected_insufficient_amount",
                ErrorCode = PaymentErrorCodes.PaymentDeclined,
                MpOrderId = "mp_r"
            }
        };
        var queue = new FakeQueue();
        var service = CreateService(context, mp, repo, queue: queue);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id), CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Empty(context.Contributions);
        Assert.Single(repo.Saved);
        Assert.Equal("rejected", repo.Saved[0].Status);
        Assert.Null(repo.Saved[0].ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Single(queue.Items);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarValidationError_QuandoCampoObrigatorioFaltando()
    {
        var context = CreateContext();
        var queue = new FakeQueue();
        var service = CreateService(context, new FakeMercadoPago(), new FakePaymentRepository(), queue: queue);

        var request = Card(Guid.NewGuid());
        request.PayerEmail = "";

        var result = await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(context.Contributions);
        Assert.Equal(2, queue.Items.Count);
    }

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveCriarContribuicaoPendente_SemConsumirPresente()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var repo = new FakePaymentRepository();
        var mp = new FakeMercadoPago
        {
            PixResult = new PaymentResponseDto { Status = "pending", MpOrderId = "mp_pix", QrCodeBase64 = "qr==" }
        };
        var queue = new FakeQueue();
        var service = CreateService(context, mp, repo, queue: queue);

        var result = await service.ProcessPixPaymentAsync(Pix(gift.Id), CancellationToken.None);

        Assert.Equal("pending", result.Status);
        var contribution = Assert.Single(context.Contributions);
        Assert.Equal(ContributionStatus.Pending, contribution.Status);
        Assert.True(context.Gifts.Single().Available);
        Assert.Single(queue.Items);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_DevePromoverContribuicaoSemIndisponibilizarPresente_QuandoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);

        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            GiftId = gift.Id,
            ContributorName = "Ana",
            Amount = 200m,
            PaymentMethod = "pix",
            Status = ContributionStatus.Pending
        };
        context.Contributions.Add(contribution);
        await context.SaveChangesAsync();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = gift.Id,
            OrderId = "ord_1",
            MpOrderId = "mp_pix",
            ContributionId = contribution.Id,
            Status = "pending"
        };
        var repo = new FakePaymentRepository { ByMpOrderId = payment };
        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), repo, email);

        await service.ConfirmPaymentAsync("mp_pix", "approved", CancellationToken.None);

        Assert.Equal("ord_1", repo.LastUpdateOrderId);
        Assert.Equal("approved", repo.LastUpdateStatus);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(1, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NaoDeveNotificarNemPromover_QuandoNaoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);

        var contribution = new Contribution
        {
            Id = Guid.NewGuid(),
            GiftId = gift.Id,
            ContributorName = "Ana",
            Amount = 200m,
            PaymentMethod = "pix",
            Status = ContributionStatus.Pending
        };
        context.Contributions.Add(contribution);
        await context.SaveChangesAsync();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = gift.Id,
            OrderId = "ord_2",
            MpOrderId = "mp_pix2",
            ContributionId = contribution.Id,
            Status = "pending"
        };
        var repo = new FakePaymentRepository { ByMpOrderId = payment };
        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), repo, email);

        await service.ConfirmPaymentAsync("mp_pix2", "rejected", CancellationToken.None);

        Assert.Equal("rejected", repo.LastUpdateStatus);
        Assert.Equal(ContributionStatus.Pending, context.Contributions.Single().Status);
        Assert.Equal(0, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_DeveSerNoOp_QuandoPagamentoNaoEncontrado()
    {
        var context = CreateContext();
        var repo = new FakePaymentRepository { ByMpOrderId = null };
        var service = CreateService(context, new FakeMercadoPago(), repo);

        await service.ConfirmPaymentAsync("inexistente", "approved", CancellationToken.None);

        Assert.Null(repo.LastUpdateStatus);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PaymentService CreateService(AppDbContext context, IMercadoPagoService mp, IPaymentRepository repo, IEmailService? email = null, IBackgroundTaskQueue? queue = null) =>
        new(mp, repo, new ContributionService(context), email ?? new FakeEmail(), queue ?? new FakeQueue(), NullLogger<PaymentService>.Instance);

    private static Gift SeedGift(AppDbContext context, decimal total = 500m)
    {
        var gift = new Gift { Id = Guid.NewGuid(), Name = "Jogo de panelas", Total = total, Available = true };
        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }

    private static CardPaymentRequestDto Card(Guid giftId, decimal amount = 100m) => new()
    {
        GiftId = giftId,
        ContributorName = "Ana",
        CardToken = "tok_123",
        OrderId = Guid.NewGuid().ToString(),
        Amount = amount,
        Installments = 1,
        Method = "credit_card",
        PaymentMethodId = "visa",
        PayerEmail = "ana@test.com",
        PayerDocNumber = "12345678909"
    };

    private static PixPaymentRequestDto Pix(Guid giftId, decimal amount = 100m) => new()
    {
        GiftId = giftId,
        ContributorName = "Ana",
        OrderId = Guid.NewGuid().ToString(),
        Amount = amount,
        PayerEmail = "ana@test.com",
        PayerDocNumber = "12345678909"
    };

    private sealed class FakeMercadoPago : IMercadoPagoService
    {
        public PaymentResponseDto CardResult = new() { Status = "approved", MpOrderId = "mp" };
        public PaymentResponseDto PixResult = new() { Status = "pending", MpOrderId = "mp" };
        public PaymentResponseDto StatusResult = new() { Status = "approved", MpOrderId = "mp" };

        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(CardResult);

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(PixResult);

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.FromResult(StatusResult);
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public readonly List<Payment> Saved = [];
        public Payment? ByMpOrderId;
        public string? LastUpdateOrderId;
        public string? LastUpdateStatus;

        public Task SaveAsync(Payment payment, CancellationToken cancellationToken)
        {
            Saved.Add(payment);
            return Task.CompletedTask;
        }

        public Task<Payment?> GetByNsuAsync(string nsu, CancellationToken cancellationToken)
            => Task.FromResult<Payment?>(null);

        public Task<Payment?> GetByMpOrderIdAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.FromResult(ByMpOrderId);

        public Task<Payment?> GetByOrderIdAsync(string orderId, CancellationToken cancellationToken)
            => Task.FromResult(Saved.FirstOrDefault(x => x.OrderId == orderId));

        public Task UpdateStatusAsync(string orderId, string status, string? statusDetail, CancellationToken cancellationToken)
        {
            LastUpdateOrderId = orderId;
            LastUpdateStatus = status;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmail : IEmailService
    {
        public int NotificationCount;
        public int AttemptCount;

        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken = default)
        {
            NotificationCount++;
            return Task.CompletedTask;
        }

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken = default)
        {
            AttemptCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueue : IBackgroundTaskQueue
    {
        public readonly List<Func<IServiceProvider, CancellationToken, Task>> Items = [];

        public ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            Items.Add(workItem);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
