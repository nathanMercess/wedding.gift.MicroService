#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        var email = new FakeEmail();
        var service = CreateService(context, mp, email);

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
        Assert.Equal(1, email.AttemptCount);
        Assert.Equal(1, email.NotificationCount);
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
    public async Task ProcessCardPaymentAsync_DeveRetornarSaldoInsuficiente_QuandoAmountMaiorQueRestante()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        context.Contributions.Add(Contribution.Create(gift.Id, "Bruno", string.Empty, 100m, "pix", DateTime.UtcNow, ContributionStatus.Paid));
        await context.SaveChangesAsync(CancellationToken.None);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 401m), CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.InsufficientAmount, result.ErrorCode);
        Assert.Equal(400m, result.RemainingAmount);
        Assert.Null(mp.LastCardRequest);
        Assert.Single(context.Contributions);
        Assert.Empty(context.Payments);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveAceitarPresenteIndisponivel_QuandoModoPrivadoIlimitado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m, available: false);
        SeedCouple(context, GiftDisplayModes.PrivateUnlimited);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 500m), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(500m, mp.LastCardRequest.Amount);
        Assert.Equal(500m, context.Contributions.Single().Amount);
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
        var email = new FakeEmail();
        var service = CreateService(context, mp, email);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 128.63m), CancellationToken.None);

        Assert.Equal("rejected", result.Status);
        Assert.Empty(context.Contributions);
        Assert.Single(context.Payments);
        Assert.Equal("rejected", context.Payments.Single().Status);
        Assert.Null(context.Payments.Single().ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(1, email.AttemptCount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_NaoDeveCriarContribuicao_QuandoEmAnalise()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var mp = new FakeMercadoPago
        {
            CardResult = new PaymentResponseDto
            {
                Status = "in_process",
                StatusDetail = "pending_contingency",
                MpOrderId = "mp_review"
            }
        };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 100m), CancellationToken.None);

        Assert.Equal("in_process", result.Status);
        Assert.Equal("pending_contingency", result.StatusDetail);
        Assert.False(result.ContributionCreated);
        Assert.Empty(context.Contributions);
        Assert.Equal("in_process", context.Payments.Single().Status);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarValidationError_QuandoCampoObrigatorioFaltando()
    {
        var context = CreateContext();
        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), email);

        var request = Card(Guid.NewGuid());
        request.PayerEmail = "";

        var result = await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        Assert.Equal("error", result.Status);
        Assert.Equal(PaymentErrorCodes.ValidationError, result.ErrorCode);
        Assert.Empty(context.Contributions);
        Assert.Equal(1, email.AttemptCount);
        Assert.Equal(1, email.ErrorCount);
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
        var email = new FakeEmail();
        var service = CreateService(context, mp, email);

        var result = await service.ProcessPixPaymentAsync(Pix(gift.Id), CancellationToken.None);

        Assert.Equal("pending", result.Status);
        Assert.Empty(context.Contributions);
        var payment = Assert.Single(context.Payments);
        Assert.Equal("pending", payment.Status);
        Assert.False(payment.ContributionCreated);
        Assert.Null(payment.ContributionId);
        Assert.True(context.Gifts.Single().Available);
        Assert.Equal(1, email.AttemptCount);
    }

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveRetornarIntencaoExistente_QuandoOrderIdReenviado()
    {
        var context = CreateContext();
        var gift = SeedGift(context);
        var request = Pix(gift.Id);
        var mp = new FakeMercadoPago
        {
            PixResult = new PaymentResponseDto
            {
                Status = "pending",
                MpOrderId = "mp_pix",
                QrCode = "qr",
                QrCodeBase64 = "qr=="
            },
            StatusResult = new PaymentResponseDto
            {
                Status = "pending",
                MpOrderId = "mp_pix",
                QrCode = "qr",
                QrCodeBase64 = "qr=="
            }
        };
        var service = CreateService(context, mp);

        var first = await service.ProcessPixPaymentAsync(request, CancellationToken.None);
        var second = await service.ProcessPixPaymentAsync(request, CancellationToken.None);

        Assert.Equal("pending", first.Status);
        Assert.Equal("pending", second.Status);
        Assert.Equal(request.OrderId, second.OrderId);
        Assert.Equal("qr==", second.QrCodeBase64);
        Assert.Equal(1, mp.PixCreateCount);
        Assert.Single(context.Payments);
        Assert.Empty(context.Contributions);
    }

    [Fact]
    public async Task GetPaymentOrderAsync_DeveRetornarRecibo_QuandoPagamentoAprovado()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 500m);
        var request = Card(gift.Id, amount: 500m);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_receipt" } };
        var service = CreateService(context, mp);

        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        var receipt = await service.GetPaymentOrderAsync(request.OrderId, CancellationToken.None);

        Assert.Equal(request.OrderId, receipt.OrderId);
        Assert.Equal(gift.Name, receipt.GiftName);
        Assert.Equal(500m, receipt.Amount);
        Assert.Equal("approved", receipt.Status);
        Assert.True(receipt.ContributionCreated);
        Assert.NotNull(receipt.PaidAt);
        Assert.Equal(request.ContributorName, receipt.ContributorName);
    }

    [Fact]
    public async Task GetPaymentStatusAsync_DeveAtualizarIntencaoECriarContribuicao_QuandoProviderAprova()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);
        var payment = Payment.CreatePix(gift.Id, gift.Name, "Ana", "Pix", "ana@test.com", "CPF", "12345678909", "ord_status", 200m, "pending", null, "mp_status", null, string.Empty, null);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);
        var mp = new FakeMercadoPago
        {
            StatusResult = new PaymentResponseDto
            {
                Status = "approved",
                MpOrderId = "mp_status",
                StatusDetail = "accredited"
            }
        };
        var service = CreateService(context, mp);

        var result = await service.GetPaymentStatusAsync("mp_status", CancellationToken.None);

        Assert.Equal("ord_status", result.OrderId);
        Assert.Equal("mp_status", result.MpOrderId);
        Assert.Equal("approved", result.Status);
        Assert.Equal("accredited", result.StatusDetail);
        Assert.True(result.ContributionCreated);
        Assert.Equal("Pix", result.Message);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
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
    public async Task ReconcileApprovedPaymentsAsync_DeveCriarContribuicoes_QuandoAprovadosSemContribuicao()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 500m);
        Payment pixPayment = Payment.CreatePix(gift.Id, "Ana", "Pix", "ana@test.com", "CPF", "12345678909", "ord_pix", 200m, "approved", null, "mp_pix", null, string.Empty, null);
        Payment cardPayment = Payment.CreateCard(gift.Id, "Bruno", "Cartao", "bruno@test.com", "CPF", "12345678909", null, "ord_card", "credit_card", 300m, 1, "approved", null, "mp_card", null);
        FakeEmail email = new();
        PaymentService service = CreateService(context, new FakeMercadoPago(), email);

        context.Payments.AddRange(pixPayment, cardPayment);
        await context.SaveChangesAsync(CancellationToken.None);

        PaymentReconciliationResponseDto result = await service.ReconcileApprovedPaymentsAsync(CancellationToken.None);

        Assert.Equal(2, result.CheckedCount);
        Assert.Equal(2, result.CreatedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(2, context.Contributions.Count());
        Assert.All(context.Payments, payment => Assert.True(payment.ContributionCreated));
        Assert.Contains(context.Contributions, contribution => contribution.PaymentMethod == "pix" && contribution.Amount == 200m);
        Assert.Contains(context.Contributions, contribution => contribution.PaymentMethod == "credit_card" && contribution.Amount == 300m);
        Assert.Equal(2, email.NotificationCount);
    }

    [Fact]
    public async Task ReconcileApprovedPaymentsAsync_DeveIgnorarPagamento_QuandoMpOrderIdNaoExiste()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 200m);
        Payment payment = Payment.CreateCard(gift.Id, "Ana", string.Empty, "ana@test.com", "CPF", "12345678909", null, "ord_card", "credit_card", 200m, 1, "approved", null, null, null);
        PaymentService service = CreateService(context, new FakeMercadoPago());

        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);

        PaymentReconciliationResponseDto result = await service.ReconcileApprovedPaymentsAsync(CancellationToken.None);

        Assert.Equal(1, result.CheckedCount);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("missing_mp_order_id", Assert.Single(result.Items).Result);
        Assert.Empty(context.Contributions);
        Assert.False(context.Payments.Single().ContributionCreated);
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
    public async Task ConfirmPaymentAsync_DeveEncontrarPagamentoPorMpPaymentId_QuandoWebhookUsaPaymentId()
    {
        var context = CreateContext();
        var gift = SeedGift(context, total: 200m);
        var payment = Payment.CreatePix(gift.Id, gift.Name, "Ana", string.Empty, "ana@test.com", "CPF", "12345678909", "ord_3", 200m, "pending", null, "mp_order_3", "mp_payment_3", string.Empty, null);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);
        var email = new FakeEmail();
        var service = CreateService(context, new FakeMercadoPago(), email);

        await service.ConfirmPaymentAsync("mp_payment_3", "approved", "accredited", "mp_payment_3", CancellationToken.None);

        Payment savedPayment = context.Payments.Single();
        Assert.Equal("approved", savedPayment.Status);
        Assert.Equal("accredited", savedPayment.StatusDetail);
        Assert.True(savedPayment.ContributionCreated);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
        Assert.Equal(1, email.NotificationCount);
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

    private static PaymentService CreateService(AppDbContext context, IMercadoPagoService mp, IEmailService? email = null)
    {
        var giftRepository = new GiftRepository(context);
        var contributionRepository = new ContributionRepository(context);
        var coupleRepository = new CoupleRepository(context);
        var paymentRepository = new PaymentRepository(context);
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        var cacheService = new ApplicationCacheService(cache);

        return new(
            mp,
            paymentRepository,
            giftRepository,
            contributionRepository,
            coupleRepository,
            email ?? new FakeEmail(),
            cacheService,
            NullLogger<PaymentService>.Instance);
    }

    private static Gift SeedGift(AppDbContext context, decimal total = 500m, bool available = true)
    {
        var gift = Gift.Create("Jogo de panelas", string.Empty, total, total, string.Empty, string.Empty, available, true);
        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }

    private static void SeedCouple(AppDbContext context, string giftDisplayMode)
    {
        Couple couple = Couple.Create();
        couple.Update(
            "Ana & Bruno",
            DateTime.UtcNow,
            string.Empty,
            string.Empty,
            string.Empty,
            "#C79A6D",
            "#F7F0EA",
            giftDisplayMode,
            null,
            null);

        context.Couples.Add(couple);
        context.SaveChanges();
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
        public int CardCreateCount;
        public int PixCreateCount;
        public int StatusRequestCount;

        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
        {
            CardCreateCount++;
            LastCardRequest = request;
            return Task.FromResult(CardResult);
        }

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
        {
            PixCreateCount++;
            return Task.FromResult(PixResult);
        }

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
        {
            StatusRequestCount++;
            return Task.FromResult(StatusResult);
        }
    }

    private sealed class FakeEmail : IEmailService
    {
        public int NotificationCount;
        public int AttemptCount;
        public int ErrorCount;

        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken)
        {
            ErrorCount++;
            return Task.CompletedTask;
        }

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

}
