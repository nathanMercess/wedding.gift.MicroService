using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;
using wedding.gift.Application.Webapi.Controllers;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using Xunit;

namespace wedding.gift.Tests;

public sealed class WebhookControllerTests
{
    [Fact]
    public void BuildSignatureManifest_DeveNormalizarDataIdAlfanumericoParaMinusculas()
    {
        MethodInfo? method = typeof(WebhookController).GetMethod("BuildSignatureManifest", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        string manifest = Assert.IsType<string>(method.Invoke(null, ["ORD-ABC123", "request-1", "123456"]));

        Assert.Equal("id:ord-abc123;request-id:request-1;ts:123456;", manifest);
    }

    [Fact]
    public async Task ReceiveMercadoPagoNotification_DeveRetornar502AntesDaJanelaDoProvider_QuandoProcessamentoExcedeOrcamento()
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["x-signature"] = "test";
        httpContext.Request.Headers["x-request-id"] = "test";
        WebhookController controller = new(
            new SlowMercadoPago(),
            null!,
            Options.Create(new MercadoPagoOptions { WebhookProcessingTimeoutSeconds = 1 }),
            new DevelopmentEnvironment(),
            NullLogger<WebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        IActionResult result = await controller.ReceiveMercadoPagoNotification(
            "ORD_TIMEOUT",
            null,
            "order",
            null,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    [Fact]
    public async Task ReceiveMercadoPagoNotification_DeveProcessarChargebackComDadosDoPagamentoOriginal()
    {
        DefaultHttpContext httpContext = new();
        RecordingPaymentService payments = new();
        WebhookController controller = new(
            new ChargebackMercadoPago(),
            payments,
            Options.Create(new MercadoPagoOptions { WebhookProcessingTimeoutSeconds = 5 }),
            new DevelopmentEnvironment(),
            NullLogger<WebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        IActionResult result = await controller.ReceiveMercadoPagoNotification(
            "CHB_1",
            null,
            "chargebacks",
            null,
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal("PAY_1", payments.ProviderId);
        Assert.Equal(PaymentStatuses.ChargedBack, payments.Status);
        Assert.Equal(100m, payments.Amount);
        Assert.Equal(100m, payments.RefundedAmount);
        Assert.Equal("BRL", payments.CurrencyId);
        Assert.Equal("credit_card", payments.Method);
    }

    private sealed class SlowMercadoPago : IMercadoPagoService
    {
        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = "pending" });

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = "pending" });

        public async Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new PaymentResponseDto { Status = "pending" };
        }

        public Task<PaymentResponseDto> GetChargebackAsync(string chargebackId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = "error" });

        public Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = "error" });

        public Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = "error" });
    }

    private sealed class ChargebackMercadoPago : IMercadoPagoService
    {
        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto
            {
                Status = PaymentStatuses.ChargedBack,
                MpPaymentId = "PAY_1",
                OrderId = "f23ebba2-764a-46b1-91c7-514f18ee4c43",
                Amount = 100m,
                CurrencyId = "BRL",
                Method = "credit_card"
            });

        public Task<PaymentResponseDto> GetChargebackAsync(string chargebackId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto
            {
                Status = PaymentStatuses.ChargedBack,
                StatusDetail = "in_process",
                MpPaymentId = "PAY_1",
                RefundedAmount = 100m
            });

        public Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> RefundAsync(string? mpOrderId, string? mpPaymentId, decimal? amount, string idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });
    }

    private sealed class RecordingPaymentService : IPaymentService
    {
        public string? ProviderId { get; private set; }
        public string? Status { get; private set; }
        public decimal? RefundedAmount { get; private set; }
        public decimal? Amount { get; private set; }
        public string? CurrencyId { get; private set; }
        public string? Method { get; private set; }

        public Task<PaymentResponseDto> ProcessCardPaymentAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> ProcessPixPaymentAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> GetPaymentOrderAsync(string orderId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> LookupPaymentOrderAsync(string orderId, string email, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentResponseDto> GetPaymentStatusAsync(string nsu, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task<PaymentReconciliationResponseDto> ReconcileApprovedPaymentsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new PaymentReconciliationResponseDto());

        public Task<PagedResult<AdminPaymentResponseDto>> GetAdminPaymentsAsync(PaymentQueryParams query, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<AdminPaymentResponseDto>());

        public Task<PaymentResponseDto> RefundPaymentAsync(string orderId, decimal? amount, Guid idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Error });

        public Task ProcessApprovedPixPaymentAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ReconcilePendingPaymentsAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ConfirmPaymentAsync(
            string mpOrderId,
            string status,
            string? statusDetail,
            string? mpPaymentId,
            decimal? refundedAmount,
            string? orderId,
            decimal? amount,
            string? currencyId,
            string? method,
            CancellationToken cancellationToken)
        {
            ProviderId = mpOrderId;
            Status = status;
            RefundedAmount = refundedAmount;
            Amount = amount;
            CurrencyId = currencyId;
            Method = method;
            return Task.CompletedTask;
        }
    }

    private sealed class DevelopmentEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "wedding.gift.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
