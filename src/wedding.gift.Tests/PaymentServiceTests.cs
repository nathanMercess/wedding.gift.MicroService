#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using Xunit;

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
        Assert.Equal(1, email.AttemptCount);
        Assert.Equal(1, email.NotificationCount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveCriarContribuicao_QuandoProviderRetornaSomentePaymentId()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 100m);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_ONLY_1" }
        };
        PaymentService service = CreateService(context, provider);

        PaymentResponseDto result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 100m), CancellationToken.None);

        Assert.Equal(PaymentStatuses.Approved, result.Status);
        Assert.Equal("PAY_ONLY_1", result.MpPaymentId);
        Assert.True(result.ContributionCreated);
        Assert.Single(context.Contributions);
        Assert.True(context.Payments.Single().ContributionCreated);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetomarIntencaoSemProvider_QuandoPrimeiraChamadaFalha()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 100m);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        FakeMercadoPago provider = new()
        {
            CardResultFactory = attempt => attempt == 1
                ? new PaymentResponseDto
                {
                    Status = PaymentStatuses.Error,
                    ErrorCode = PaymentErrorCodes.ProviderError,
                    Message = "timeout"
                }
                : new PaymentResponseDto
                {
                    Status = PaymentStatuses.Approved,
                    MpPaymentId = "PAY_RETRY_1"
                }
        };
        PaymentService service = CreateService(context, provider);

        PaymentResponseDto first = await service.ProcessCardPaymentAsync(request, CancellationToken.None);
        PaymentResponseDto second = await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, first.Status);
        Assert.Equal(PaymentStatuses.Approved, second.Status);
        Assert.Equal(2, provider.CardCreateCount);
        Assert.Single(context.Payments);
        Assert.Single(context.Contributions);
        Assert.Equal("PAY_RETRY_1", context.Payments.Single().MpPaymentId);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarAprovadoParaChamadaConcorrenteQueRecebeResourceLocked()
    {
        InMemoryDatabaseRoot databaseRoot = new();
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), databaseRoot)
            .Options;
        await using AppDbContext firstContext = new(options);
        await using AppDbContext secondContext = new(options);
        Gift gift = SeedGift(firstContext, total: 100m);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        TaskCompletionSource firstProviderStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondProviderStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirstProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecondProvider = new(TaskCreationOptions.RunContinuationsAsynchronously);
        FakeMercadoPago provider = new()
        {
            CardResultAsyncFactory = async attempt =>
            {
                if (attempt == 1)
                {
                    firstProviderStarted.TrySetResult();
                    await releaseFirstProvider.Task;
                    return new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_CONCURRENT" };
                }

                secondProviderStarted.TrySetResult();
                await releaseSecondProvider.Task;
                return new PaymentResponseDto
                {
                    Status = PaymentStatuses.Error,
                    ErrorCode = PaymentErrorCodes.ResourceLocked
                };
            }
        };
        PaymentService firstService = CreateService(firstContext, provider);
        PaymentService secondService = CreateService(secondContext, provider);

        Task<PaymentResponseDto> firstTask = firstService.ProcessCardPaymentAsync(request, CancellationToken.None);
        await firstProviderStarted.Task;
        Task<PaymentResponseDto> secondTask = secondService.ProcessCardPaymentAsync(request, CancellationToken.None);
        await secondProviderStarted.Task;
        releaseFirstProvider.TrySetResult();
        PaymentResponseDto first = await firstTask;
        releaseSecondProvider.TrySetResult();
        PaymentResponseDto second = await secondTask;

        Assert.Equal(PaymentStatuses.Approved, first.Status);
        Assert.Equal(PaymentStatuses.Approved, second.Status);
        Assert.True(second.ContributionCreated);
        Assert.Null(second.ErrorCode);
        Assert.Equal(2, provider.CardCreateCount);
        Assert.Single(firstContext.Payments);
        Assert.Single(firstContext.Contributions);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_DeveRetornarInProcessQuandoResourceLockedAindaNaoFoiLiquidado()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 100m);
        FakeEmail email = new();
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Error,
                ErrorCode = PaymentErrorCodes.ResourceLocked
            }
        };
        PaymentService service = CreateService(context, provider, email);

        PaymentResponseDto result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 100m), CancellationToken.None);

        Assert.Equal(PaymentStatuses.InProcess, result.Status);
        Assert.Equal("provider_lock_retry", result.StatusDetail);
        Assert.Null(result.ErrorCode);
        Assert.Equal(0, email.ErrorCount);
        Assert.Equal(PaymentStatuses.Pending, context.Payments.Single().Status);
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
        var gift = SeedGift(context, total: 500m);
        SeedPaidContribution(context, gift.Id, 500m);
        SeedCouple(context, GiftDisplayModes.PrivateUnlimited);
        var mp = new FakeMercadoPago { CardResult = new PaymentResponseDto { Status = "approved", MpOrderId = "mp_1" } };
        var service = CreateService(context, mp);

        var result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 500m), CancellationToken.None);

        Assert.Equal("approved", result.Status);
        Assert.Equal(500m, mp.LastCardRequest.Amount);
        Assert.Equal(1_000m, context.Contributions.Sum(x => x.Amount));
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
    public async Task GetPaymentOrderAsync_DeveNormalizarExpiracaoComoUtc()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        DateTime expiresAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(30), DateTimeKind.Unspecified);
        Payment payment = Payment.CreatePix(
            gift.Id,
            gift.Name,
            "Ana",
            "Pix",
            "ana@test.com",
            "CPF",
            "12345678909",
            Guid.NewGuid().ToString(),
            100m,
            PaymentStatuses.Pending,
            "pending_waiting_transfer",
            null,
            null,
            "pix-code",
            "qr==",
            expiresAt);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);
        PaymentService service = CreateService(context, new FakeMercadoPago());

        PaymentResponseDto result = await service.GetPaymentOrderAsync(payment.OrderId, CancellationToken.None);

        Assert.NotNull(result.ExpiresAt);
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAt.Value.Kind);
        Assert.Equal(expiresAt, result.ExpiresAt.Value);
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
                StatusDetail = "accredited",
                Amount = 200m,
                CurrencyId = "BRL",
                Method = "pix"
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
    public async Task GetPaymentStatusAsync_NaoDeveConsultarProvider_QuandoIdentificadorNaoPertenceAoSistema()
    {
        AppDbContext context = CreateContext();
        FakeMercadoPago provider = new();
        PaymentService service = CreateService(context, provider);

        PaymentResponseDto result = await service.GetPaymentStatusAsync("PAY_UNKNOWN", CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, result.Status);
        Assert.Equal(PaymentErrorCodes.OrderNotFound, result.ErrorCode);
        Assert.Equal(0, provider.StatusRequestCount);
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
        Assert.Equal("missing_provider_id", Assert.Single(result.Items).Result);
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

        await service.ConfirmPaymentAsync("mp_pix2", "rejected", null, null, null, null, null, null, null, CancellationToken.None);

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

        await service.ConfirmPaymentAsync("mp_payment_3", "approved", "accredited", "mp_payment_3", null, null, 200m, "BRL", "pix", CancellationToken.None);

        Payment savedPayment = context.Payments.Single();
        Assert.Equal("approved", savedPayment.Status);
        Assert.Equal("accredited", savedPayment.StatusDetail);
        Assert.True(savedPayment.ContributionCreated);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
        Assert.Equal(1, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_DeveCorrelacionarPorOrderId_QuandoReservaAindaNaoTemProviderId()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, total: 200m);
        Payment payment = Payment.CreatePix(
            gift.Id,
            gift.Name,
            "Ana",
            string.Empty,
            "ana@test.com",
            "CPF",
            "12345678909",
            "order_without_provider",
            200m,
            PaymentStatuses.Pending,
            null,
            null,
            null,
            string.Empty,
            null);
        context.Payments.Add(payment);
        await context.SaveChangesAsync(CancellationToken.None);
        FakeEmail email = new();
        PaymentService service = CreateService(context, new FakeMercadoPago(), email);

        await service.ConfirmPaymentAsync(
            "ORD_PROVIDER_RECOVERED",
            PaymentStatuses.Approved,
            "accredited",
            "PAY_PROVIDER_RECOVERED",
            null,
            payment.OrderId,
            200m,
            "BRL",
            "pix",
            CancellationToken.None);
        await service.ConfirmPaymentAsync(
            "ORD_PROVIDER_RECOVERED",
            PaymentStatuses.Approved,
            "accredited",
            "PAY_PROVIDER_RECOVERED",
            null,
            payment.OrderId,
            200m,
            "BRL",
            "pix",
            CancellationToken.None);

        Payment savedPayment = context.Payments.Single();
        Assert.Equal("ORD_PROVIDER_RECOVERED", savedPayment.MpOrderId);
        Assert.Equal("PAY_PROVIDER_RECOVERED", savedPayment.MpPaymentId);
        Assert.True(savedPayment.ContributionCreated);
        Assert.Single(context.Contributions);
        Assert.Equal(1, email.NotificationCount);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_DeveSerNoOp_QuandoPagamentoNaoEncontrado()
    {
        var context = CreateContext();
        var service = CreateService(context, new FakeMercadoPago());

        await service.ConfirmPaymentAsync("inexistente", "approved", null, null, null, null, null, null, null, CancellationToken.None);

        Assert.Empty(context.Payments);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveManterReserva_QuandoProviderRetornaActionRequired()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            PixResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.ActionRequired,
                StatusDetail = "waiting_transfer",
                MpOrderId = "ORD_PIX",
                QrCode = "pix-code"
            }
        };
        PaymentService service = CreateService(context, provider);

        PaymentResponseDto result = await service.ProcessPixPaymentAsync(Pix(gift.Id), CancellationToken.None);

        Assert.Equal(PaymentStatuses.ActionRequired, result.Status);
        Assert.Equal(PaymentStatuses.ActionRequired, context.Payments.Single().Status);
        Assert.Empty(context.Contributions);
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveReverterContribuicaoPaga()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpOrderId = "PAY_1", MpPaymentId = "PAY_1" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.Refunded, MpPaymentId = "PAY_1" }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        PaymentResponseDto result = await service.RefundPaymentAsync(request.OrderId, null, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(PaymentStatuses.Refunded, result.Status);
        Assert.Equal(ContributionStatus.Refunded, context.Contributions.Single().Status);
        Assert.Equal(1, provider.RefundCount);
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveManterSaldoLiquido_QuandoReembolsoParcial()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpOrderId = "PAY_2", MpPaymentId = "PAY_2" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.PartiallyRefunded, MpPaymentId = "PAY_2" }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        PaymentResponseDto result = await service.RefundPaymentAsync(request.OrderId, 35m, Guid.NewGuid(), CancellationToken.None);

        Contribution contribution = context.Contributions.Single();
        Assert.Equal(PaymentStatuses.PartiallyRefunded, result.Status);
        Assert.Equal(35m, result.RefundedAmount);
        Assert.Equal(ContributionStatus.Paid, contribution.Status);
        Assert.Equal(35m, contribution.RefundedAmount);
        Assert.Equal(65m, contribution.NetAmount);
        Assert.Equal(35m, provider.LastRefundAmount);
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveRegistrarAuditoriaComAtorUmaUnicaVez()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        Guid operationId = Guid.NewGuid();
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_AUDIT" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.PartiallyRefunded, MpPaymentId = "PAY_AUDIT" }
        };
        FakeRequestContext requestContext = new(gift.CoupleId);
        PaymentService service = CreateService(
            context,
            provider,
            requestContext: requestContext,
            operationalRepository: new OperationalRepository(context));
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        await service.RefundPaymentAsync(request.OrderId, 35m, operationId, CancellationToken.None);
        await service.RefundPaymentAsync(request.OrderId, 35m, operationId, CancellationToken.None);

        AuditLog auditLog = Assert.Single(context.AuditLogs);
        PaymentRefundOperation refundOperation = Assert.Single(context.PaymentRefundOperations);
        Assert.Equal(requestContext.UserId, auditLog.UserId);
        Assert.Equal(gift.CoupleId, auditLog.CoupleId);
        Assert.Equal("PaymentPartiallyRefunded", auditLog.Action);
        Assert.Equal("PaymentRefundOperation", auditLog.EntityType);
        Assert.Equal(refundOperation.Id.ToString(), auditLog.EntityId);
        Assert.Equal(requestContext.CorrelationId, auditLog.CorrelationId);
    }

    [Fact]
    public async Task RefundPaymentAsync_NaoDeveSomarDuasVezes_QuandoMesmaOperacaoForReenviada()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        Guid operationId = Guid.NewGuid();
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_REFUND_RETRY" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.PartiallyRefunded, MpPaymentId = "PAY_REFUND_RETRY" },
            StatusResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.PartiallyRefunded,
                MpPaymentId = "PAY_REFUND_RETRY",
                RefundedAmount = 35m
            }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        PaymentResponseDto first = await service.RefundPaymentAsync(request.OrderId, 35m, operationId, CancellationToken.None);
        PaymentResponseDto second = await service.RefundPaymentAsync(request.OrderId, 35m, operationId, CancellationToken.None);

        Assert.Equal(35m, first.RefundedAmount);
        Assert.Equal(35m, second.RefundedAmount);
        Assert.Equal(35m, context.Payments.Single().RefundedAmount);
        Assert.Equal(35m, context.Contributions.Single().RefundedAmount);
        Assert.Equal(1, provider.RefundCount);
        Assert.Single(provider.RefundIdempotencyKeys);
        Assert.All(provider.RefundIdempotencyKeys, key => Assert.Equal(operationId.ToString("D"), key));
    }

    [Fact]
    public async Task RefundPaymentAsync_NaoDeveReverterParcialParaPago_QuandoConsultaDoProviderAindaNaoTrazReembolso()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_PARTIAL_STALE" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.PartiallyRefunded, MpPaymentId = "PAY_PARTIAL_STALE" },
            StatusResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_PARTIAL_STALE", RefundedAmount = 0m }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);
        await service.RefundPaymentAsync(request.OrderId, 35m, Guid.NewGuid(), CancellationToken.None);

        PaymentResponseDto result = await service.GetPaymentStatusAsync("PAY_PARTIAL_STALE", CancellationToken.None);

        Assert.Equal(PaymentStatuses.PartiallyRefunded, result.Status);
        Assert.Equal(35m, result.RefundedAmount);
        Assert.Equal(35m, context.Contributions.Single().RefundedAmount);
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveRejeitarChaveJaUsadaComOutroValorSemNovoReembolso()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_REFUND_KEY" },
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.PartiallyRefunded, MpPaymentId = "PAY_REFUND_KEY" }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        Guid operationId = Guid.NewGuid();
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);
        await service.RefundPaymentAsync(request.OrderId, 35m, operationId, CancellationToken.None);

        ConflictException exception = await Assert.ThrowsAsync<ConflictException>(
            () => service.RefundPaymentAsync(request.OrderId, 10m, operationId, CancellationToken.None));

        Assert.Equal(PaymentErrorCodes.IdempotencyKeyAlreadyUsed, exception.Code);
        Assert.Equal(1, provider.RefundCount);
        Assert.Single(context.PaymentRefundOperations);
    }

    [Fact]
    public async Task RefundPaymentAsync_DeveRecuperarSucessoAnteriorQuandoProviderRetornaChaveJaUsada()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_REFUND_RECOVERY" },
            RefundResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Error,
                ErrorCode = PaymentErrorCodes.IdempotencyKeyAlreadyUsed
            },
            StatusResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Approved,
                MpPaymentId = "PAY_REFUND_RECOVERY",
                RefundedAmount = 35m
            }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        PaymentResponseDto result = await service.RefundPaymentAsync(request.OrderId, 35m, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(PaymentStatuses.PartiallyRefunded, result.Status);
        Assert.Equal(35m, result.RefundedAmount);
        Assert.Single(context.PaymentRefundOperations);
    }

    [Fact]
    public async Task GetAdminPaymentsAsync_DeveExporValoresEstornadoERestante()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        Payment payment = Payment.CreateCard(
            gift.Id,
            gift.Name,
            "Ana",
            string.Empty,
            "ana@test.com",
            "CPF",
            "12345678909",
            null,
            Guid.NewGuid().ToString(),
            "credit_card",
            100m,
            1,
            PaymentStatuses.Approved,
            null,
            "ORDER_ADMIN",
            "PAY_ADMIN");
        payment.UpdateProviderStatus(
            PaymentStatuses.PartiallyRefunded,
            PaymentStatuses.PartiallyRefunded,
            refundedAmount: 35m);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        PaymentService service = CreateService(context, new FakeMercadoPago());

        PagedResult<AdminPaymentResponseDto> result = await service.GetAdminPaymentsAsync(
            new PaymentQueryParams(),
            CancellationToken.None);

        AdminPaymentResponseDto item = Assert.Single(result.Items);
        Assert.Equal(100m, item.Amount);
        Assert.Equal(35m, item.RefundedAmount);
        Assert.Equal(65m, item.RemainingAmount);
    }

    [Fact]
    public async Task ProcessCardPaymentAsync_NaoDeveLiquidarQuandoValorDoProviderDiverge()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Approved,
                MpPaymentId = "PAY_MISMATCH",
                Amount = 99m,
                CurrencyId = "BRL",
                Method = "credit_card"
            }
        };
        PaymentService service = CreateService(context, provider);

        PaymentResponseDto result = await service.ProcessCardPaymentAsync(Card(gift.Id, amount: 100m), CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, result.Status);
        Assert.Equal(PaymentErrorCodes.ProviderDataMismatch, result.ErrorCode);
        Assert.Equal(PaymentStatuses.Pending, context.Payments.Single().Status);
        Assert.Empty(context.Contributions);
    }

    [Fact]
    public async Task ReconcilePendingPaymentsAsync_DeveAplicarChargebackERecuperarCoberturaFavoravel()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new()
        {
            CardResult = new PaymentResponseDto { Status = PaymentStatuses.Approved, MpPaymentId = "PAY_CHARGEBACK" },
            StatusResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.ChargedBack,
                StatusDetail = "in_process",
                MpPaymentId = "PAY_CHARGEBACK",
                RefundedAmount = 100m
            }
        };
        PaymentService service = CreateService(context, provider);
        CardPaymentRequestDto request = Card(gift.Id, amount: 100m);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        await service.ReconcilePendingPaymentsAsync(CancellationToken.None);

        Assert.Equal(PaymentStatuses.ChargedBack, context.Payments.Single().Status);
        Assert.Equal(ContributionStatus.Chargeback, context.Contributions.Single().Status);
        Assert.Equal(0m, context.Contributions.Single().NetAmount);

        await service.ConfirmPaymentAsync(
            "PAY_CHARGEBACK",
            PaymentStatuses.ChargedBack,
            "reimbursed",
            "PAY_CHARGEBACK",
            0m,
            request.OrderId,
            100m,
            "BRL",
            "credit_card",
            CancellationToken.None);

        Assert.Equal(PaymentStatuses.Approved, context.Payments.Single().Status);
        Assert.Equal(ContributionStatus.Paid, context.Contributions.Single().Status);
        Assert.Equal(100m, context.Contributions.Single().NetAmount);
    }

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveRejeitarOrderIdReutilizadoComOutroValor()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new();
        PaymentService service = CreateService(context, provider);
        PixPaymentRequestDto first = Pix(gift.Id, 100m);
        await service.ProcessPixPaymentAsync(first, CancellationToken.None);
        PixPaymentRequestDto second = Pix(gift.Id, 120m);
        second.OrderId = first.OrderId;

        PaymentResponseDto result = await service.ProcessPixPaymentAsync(second, CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, result.Status);
        Assert.Equal(PaymentErrorCodes.DuplicateOrder, result.ErrorCode);
        Assert.Single(context.Payments);
    }

    [Fact]
    public async Task ProcessPixPaymentAsync_DeveRejeitarOrderIdReutilizadoComOutroPagador()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context);
        FakeMercadoPago provider = new();
        PaymentService service = CreateService(context, provider);
        PixPaymentRequestDto first = Pix(gift.Id, 100m);
        await service.ProcessPixPaymentAsync(first, CancellationToken.None);
        PixPaymentRequestDto second = Pix(gift.Id, 100m);
        second.OrderId = first.OrderId;
        second.ContributorName = "Outra pessoa";

        PaymentResponseDto result = await service.ProcessPixPaymentAsync(second, CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, result.Status);
        Assert.Equal(PaymentErrorCodes.DuplicateOrder, result.ErrorCode);
        Assert.Equal(1, provider.PixCreateCount);
    }

    private static PaymentService CreateService(
        AppDbContext context,
        IMercadoPagoService mp,
        IEmailService? email = null,
        IRequestContext? requestContext = null,
        IOperationalRepository? operationalRepository = null)
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
            NullLogger<PaymentService>.Instance,
            requestContext: requestContext,
            operationalRepository: operationalRepository);
    }

    private static Gift SeedGift(AppDbContext context, decimal total = 500m)
    {
        var gift = Gift.Create("Jogo de panelas", string.Empty, total, total, string.Empty, string.Empty, true);
        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }

    private static void SeedPaidContribution(AppDbContext context, Guid giftId, decimal amount)
    {
        context.Contributions.Add(Contribution.Create(
            giftId,
            "Convidado anterior",
            string.Empty,
            amount,
            "pix",
            DateTime.UtcNow,
            ContributionStatus.Paid));
        context.SaveChanges();
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
        public PaymentResponseDto RefundResult = new() { Status = "refunded", MpOrderId = "mp" };
        public Func<int, PaymentResponseDto>? CardResultFactory;
        public Func<int, Task<PaymentResponseDto>>? CardResultAsyncFactory;
        public CardPaymentRequestDto LastCardRequest = null!;
        public PixPaymentRequestDto LastPixRequest = null!;
        public int CardCreateCount;
        public int PixCreateCount;
        public int StatusRequestCount;
        public int RefundCount;
        public decimal? LastRefundAmount;
        public List<string> RefundIdempotencyKeys = [];

        public async Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
        {
            int attempt = Interlocked.Increment(ref CardCreateCount);
            LastCardRequest = request;
            PaymentResponseDto result = CardResultAsyncFactory is null
                ? CardResultFactory?.Invoke(attempt) ?? CardResult
                : await CardResultAsyncFactory(attempt);
            CompleteProviderData(result, request.Amount, request.Method);
            return result;
        }

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
        {
            PixCreateCount++;
            LastPixRequest = request;
            CompleteProviderData(PixResult, request.Amount, "pix");
            return Task.FromResult(PixResult);
        }

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
        {
            StatusRequestCount++;
            if (LastCardRequest is not null)
                CompleteProviderData(StatusResult, LastCardRequest.Amount, LastCardRequest.Method);

            if (LastPixRequest is not null)
                CompleteProviderData(StatusResult, LastPixRequest.Amount, "pix");

            return Task.FromResult(StatusResult);
        }

        public Task<PaymentResponseDto> GetChargebackAsync(string chargebackId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> RefundAsync(
            string? mpOrderId,
            string? mpPaymentId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            RefundCount++;
            RefundIdempotencyKeys.Add(idempotencyKey);
            return Task.FromResult(RefundResult);
        }

        public Task<PaymentResponseDto> RefundAsync(
            string? mpOrderId,
            string? mpPaymentId,
            decimal? amount,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            RefundCount++;
            LastRefundAmount = amount;
            RefundIdempotencyKeys.Add(idempotencyKey);
            return Task.FromResult(RefundResult);
        }

        private static void CompleteProviderData(PaymentResponseDto result, decimal amount, string method)
        {
            result.Amount ??= amount;
            result.CurrencyId ??= "BRL";
            result.Method = string.IsNullOrWhiteSpace(result.Method) ? method : result.Method;
        }
    }

    private sealed class FakeRequestContext(Guid coupleId) : IRequestContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? CoupleId { get; } = coupleId;
        public bool IsSuperAdmin { get; init; } = true;
        public string CorrelationId { get; } = Guid.NewGuid().ToString();
        public string RemoteIpAddress { get; } = "127.0.0.1";
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

        public Task SendGuestReceiptAsync(
            string toEmail,
            string contributorName,
            string giftName,
            string orderId,
            decimal amount,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken)
        {
            AttemptCount++;
            return Task.CompletedTask;
        }
    }

}
