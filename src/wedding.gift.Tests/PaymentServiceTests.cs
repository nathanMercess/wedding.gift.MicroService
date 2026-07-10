#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
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
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var queue = new FakeQueue();
        var service = CreateService(context, mp, queue: queue);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 500m), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(500m, mp.LastCardRequest.Amount);
        var contribution = Assert.Single(context.Contributions);
        Assert.Equal(ContributionStatus.Paid, contribution.Status);
        Assert.Equal(500m, contribution.Amount);
        Assert.Single(context.Payments);
        Assert.Equal("approved", context.Payments.Single().Status);
        Assert.NotNull(context.Payments.Single().ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(2, queue.Items.Count);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveUsarAmountParaCobrarEContribuir_QuandoCredito()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 499m, installments: 12), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(499m, mp.LastCardRequest.Amount);
        Assert.Equal(499m, context.Contributions.Single().Amount);
        Assert.Equal(499m, context.Payments.Single().Amount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveAceitarValorLiquidoIgualAoBruto_QuandoDebito()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 500m, method: "debit_card"), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(500m, mp.LastCardRequest.Amount);
        Assert.Equal(500m, context.Contributions.Single().Amount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveUsarValorBrutoDoRequest_QuandoCredito()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 49m), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(49m, mp.LastCardRequest.Amount);
        Assert.Equal(49m, context.Contributions.Single().Amount);
        Assert.Equal(49m, context.Payments.Single().Amount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarValidationError_QuandoParcelasExcedemLimiteDoPresente()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var mp = new FakeMercadoPago();
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 128.63m, installments: 13), CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.ValidationError, result.ErrorCode);
        Assert.Null(mp.LastCardRequest);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarValidationError_QuandoAmountExcedeRestante()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        context.Contributions.Add(Contribution.Create(gift.Id, "Bruno", string.Empty, 100m, "pix", DateTime.UtcNow, ContributionStatus.Paid));
        await context.SaveChangesAsync(CancellationToken.None);
        var mp = new FakeMercadoPago();
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 401m), CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.ValidationError, result.ErrorCode);
        Assert.Null(mp.LastCardRequest);
        Assert.Empty(context.Payments);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_NaoDeveCriarContribuicao_QuandoRecusado()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
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
        var service = CreateService(context, mp, queue: queue);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 128.63m), CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Empty(context.Contributions);
        Assert.Single(context.Payments);
        Assert.Equal("rejected", context.Payments.Single().Status);
        Assert.Null(context.Payments.Single().ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Single(queue.Items);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarValidationError_QuandoCampoObrigatorioFaltando()
    {
        var context = CreateContext();
        var queue = new FakeQueue();
        var service = CreateService(context, new FakeMercadoPago(), queue: queue);

        var request = Card(Guid.NewGuid());
        request.PayerEmail = "";

        var result = await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(context.Contributions);
        Assert.Equal(2, queue.Items.Count);
    }

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveSalvarIntencaoPendente_SemCriarContribuicao()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var mp = new FakeMercadoPago
        {
            PixResult = new PaymentResponseDto { Status = "pending", MpOrderId = "mp_pix", QrCodeBase64 = "qr==" }
        };
        var queue = new FakeQueue();
        var service = CreateService(context, mp, queue: queue);

        var result = await service.ProcessPixPaymentAsync(Pix(gift.Id), CancellationToken.None);

        Assert.Equal("pending", result.Status);
        Assert.Empty(context.Contributions);
        var payment = Assert.Single(context.Payments);
        Assert.Equal("pending", payment.Status);
        Assert.False(payment.ContributionCreated);
        Assert.Null(payment.ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Single(queue.Items);
    }

    [Fact]
    public async Task ProcessApprovedPixPaymentAsync_DeveCriarContribuicaoUmaVez_QuandoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);

        var payment = Payment.CreatePix(gift.Id, "Ana", "Parabens", "ana@test.com", "CPF", "12345678909", "ord_1", 200m, "approved", null, "mp_pix", null, string.Empty, null);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), email);

        await service.ProcessApprovedPixPaymentAsync("mp_pix", CancellationToken.None);
        await service.ProcessApprovedPixPaymentAsync("mp_pix", CancellationToken.None);

        var savedPayment = context.Payments.Single();
        Assert.True(savedPayment.ContributionCreated);
        Assert.NotNull(savedPayment.ContributionId);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
        Assert.Equal("Parabens", context.Contributions.Single().Message);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(1, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NaoDeveNotificarNemPromover_QuandoNaoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);

        var payment = Payment.CreatePix(gift.Id, "Ana", string.Empty, "ana@test.com", "CPF", "12345678909", "ord_2", 200m, "pending", null, "mp_pix2", null, string.Empty, null);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), email);

        await service.ConfirmPaymentAsync("mp_pix2", "rejected", CancellationToken.None);

        Assert.Equal("rejected", context.Payments.Single().Status);
        Assert.Empty(context.Contributions);
        Assert.Equal(0, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_DeveSerNoOp_QuandoPagamentoNaoEncontrado()
    {
        var context = CreateContext();
        var service = CreateService(context, new FakeMercadoPago());

        await service.ConfirmPaymentAsync("inexistente", "approved", CancellationToken.None);

        Assert.Empty(context.Payments);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PaymentService CreateService(AppDbContext context, IMercadoPagoService mp, IEmailService? email = null, IBackgroundTaskQueue? queue = null)
    {
        var giftRepository = new GiftRepository(context);
        var contributionRepository = new ContributionRepository(context);
        var paymentRepository = new PaymentRepository(context);
        var contributionService = new ContributionService(contributionRepository, giftRepository);

        return new(
            mp,
            paymentRepository,
            giftRepository,
            contributionRepository,
            contributionService,
            email ?? new FakeEmail(),
            queue ?? new FakeQueue(),
            NullLogger<PaymentService>.Instance);
    }

    private static Gift SeedGift(AppDbContext context, decimal total = 500m)
    {
        var gift = Gift.Create("Jogo de panelas", string.Empty, total, total, string.Empty, string.Empty, true, true);
        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }

    private static CardPaymentRequestDto Card(
        Guid giftId,
        decimal amount = 100m,
        int installments = 1,
        string method = "credit_card") => new()
    {
        GiftId = giftId,
        ContributorName = "Ana",
        CardToken = "tok_123",
        OrderId = Guid.NewGuid().ToString(),
        Amount = amount,
        Installments = installments,
        Method = method,
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
        public CardPaymentRequestDto LastCardRequest = null!;

        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
        {
            LastCardRequest = request;
            return Task.FromResult(CardResult);
        }

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(PixResult);

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.FromResult(StatusResult);
    }

    private sealed class FakeEmail : IEmailService
    {
        public int NotificationCount;
        public int AttemptCount;

        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken)
        {
            NotificationCount++;
            return Task.CompletedTask;
        }

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken)
        {
            AttemptCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueue : IBackgroundTaskQueue
    {
        public readonly List<Func<IServiceProvider, CancellationToken, Task>> Items = [];

        public ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> workItem, CancellationToken cancellationToken)
        {
            Items.Add(workItem);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
