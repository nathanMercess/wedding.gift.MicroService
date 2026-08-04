using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using wedding.gift.Application.Webapi.Controllers;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Contracts;
using Xunit;

namespace wedding.gift.Tests;

public sealed class PaymentControllerTests
{
    [Fact]
    public async Task GetPaymentOrder_DeveOcultarExistenciaSemCookieAssinado()
    {
        PaymentServiceStub paymentService = new()
        {
            OrderResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Pending,
                OrderId = Guid.NewGuid().ToString("D")
            }
        };
        PaymentController controller = CreateController(paymentService);

        PublicPaymentResponseDto result = await controller.GetPaymentOrder(
            Guid.Parse(paymentService.OrderResult.OrderId),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, controller.Response.StatusCode);
        Assert.Equal(PaymentErrorCodes.OrderNotFound, result.ErrorCode);
        Assert.Equal(0, paymentService.OrderLookupCount);
    }

    [Fact]
    public async Task GetPaymentOrder_NaoDeveExporDadosDoProvedorNemQrExpirado()
    {
        PaymentServiceStub paymentService = new()
        {
            OrderResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Expired,
                OrderId = Guid.NewGuid().ToString("D"),
                ContributorName = "Convidado",
                Message = "Mensagem privada",
                MpPaymentId = "provider-payment",
                MpOrderId = "provider-order",
                MpRequestId = "provider-request",
                QrCode = "expired-qr",
                QrCodeBase64 = "expired-base64",
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };
        PaymentController controller = CreateController(paymentService);
        await AuthorizeCurrentOrderAsync(controller, paymentService.OrderResult.OrderId!);

        PublicPaymentResponseDto result = await controller.GetPaymentOrder(
            Guid.Parse(paymentService.OrderResult.OrderId),
            CancellationToken.None);

        Assert.Empty(result.QrCode);
        Assert.Null(result.QrCodeBase64);
        Assert.DoesNotContain(typeof(PublicPaymentResponseDto).GetProperties(), property =>
            property.Name is "ContributorName" or "MpPaymentId" or "MpOrderId" or "MpRequestId" or "Nsu");
    }

    [Fact]
    public async Task GetPaymentOrder_DeveManterQrSomenteEnquantoPagamentoEstaAtivo()
    {
        PaymentServiceStub paymentService = new()
        {
            OrderResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Pending,
                OrderId = Guid.NewGuid().ToString("D"),
                QrCode = "active-qr",
                QrCodeBase64 = "active-base64",
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            }
        };
        PaymentController controller = CreateController(paymentService);
        await AuthorizeCurrentOrderAsync(controller, paymentService.OrderResult.OrderId!);

        PublicPaymentResponseDto result = await controller.GetPaymentOrder(
            Guid.Parse(paymentService.OrderResult.OrderId),
            CancellationToken.None);

        Assert.Equal("active-qr", result.QrCode);
        Assert.Equal("active-base64", result.QrCodeBase64);
    }

    private static PaymentController CreateController(IPaymentService paymentService)
        => new(
            paymentService,
            new OrderLookupServiceStub(),
            Options.Create(new JwtOptions { SigningKey = "test-signing-key-00000000000000000000" }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static async Task AuthorizeCurrentOrderAsync(PaymentController controller, string orderId)
    {
        await controller.PayWithPix(new PixPaymentRequestDto
        {
            GiftId = Guid.NewGuid(),
            ContributorName = "Convidado",
            OrderId = orderId,
            Amount = 10m,
            PayerEmail = "qa@example.com",
            PayerDocNumber = "12345678909"
        }, CancellationToken.None);

        string cookie = Assert.Single(controller.Response.Headers.SetCookie).Split(';')[0];
        controller.Request.Headers.Cookie = cookie;
    }

    private sealed class PaymentServiceStub : IPaymentService
    {
        public required PaymentResponseDto OrderResult { get; init; }
        public int OrderLookupCount { get; private set; }

        public Task<PaymentResponseDto> GetPaymentOrderAsync(string orderId, CancellationToken cancellationToken)
        {
            OrderLookupCount++;
            return Task.FromResult(OrderResult);
        }
        public Task<PaymentResponseDto> ProcessCardPaymentAsync(CardPaymentRequestDto request, CancellationToken cancellationToken) => Task.FromResult(OrderResult);
        public Task<PaymentResponseDto> ProcessPixPaymentAsync(PixPaymentRequestDto request, CancellationToken cancellationToken) => Task.FromResult(OrderResult);
        public Task<PaymentResponseDto> LookupPaymentOrderAsync(string orderId, string email, CancellationToken cancellationToken) => Task.FromResult(OrderResult);
        public Task<PaymentResponseDto> GetPaymentStatusAsync(string nsu, CancellationToken cancellationToken) => Task.FromResult(OrderResult);
        public Task<PaymentReconciliationResponseDto> ReconcileApprovedPaymentsAsync(CancellationToken cancellationToken) => Task.FromResult(new PaymentReconciliationResponseDto());
        public Task<PagedResult<AdminPaymentResponseDto>> GetAdminPaymentsAsync(PaymentQueryParams query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<AdminPaymentResponseDto>());
        public Task<PaymentResponseDto> RefundPaymentAsync(string orderId, decimal? amount, Guid idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(OrderResult);
        public Task ProcessApprovedPixPaymentAsync(string mpOrderId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReconcilePendingPaymentsAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ConfirmPaymentAsync(string mpOrderId, string status, string? statusDetail, string? mpPaymentId, decimal? refundedAmount, string? orderId, decimal? amount, string? currencyId, string? method, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class OrderLookupServiceStub : IOrderLookupService
    {
        public Task RequestAsync(OrderLookupRequestDto request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OrderLookupResponseDto> ConsumeAsync(string token, CancellationToken cancellationToken) => Task.FromResult(new OrderLookupResponseDto());
        public Task<string> CreateTokenAsync(Guid paymentId, CancellationToken cancellationToken) => Task.FromResult("token");
    }
}
