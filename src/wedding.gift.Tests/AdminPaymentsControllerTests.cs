using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using Xunit;

namespace wedding.gift.Tests;

public sealed class AdminPaymentsControllerTests
{
    [Theory]
    [InlineData(PaymentErrorCodes.ValidationError, StatusCodes.Status400BadRequest)]
    [InlineData(PaymentErrorCodes.OrderNotFound, StatusCodes.Status404NotFound)]
    [InlineData(PaymentErrorCodes.PaymentNotRefundable, StatusCodes.Status409Conflict)]
    [InlineData(PaymentErrorCodes.InvalidRefundAmount, StatusCodes.Status409Conflict)]
    [InlineData(PaymentErrorCodes.IdempotencyKeyAlreadyUsed, StatusCodes.Status409Conflict)]
    [InlineData(PaymentErrorCodes.ResourceLocked, StatusCodes.Status409Conflict)]
    [InlineData(PaymentErrorCodes.ProviderError, StatusCodes.Status502BadGateway)]
    [InlineData(PaymentErrorCodes.ProviderDataMismatch, StatusCodes.Status502BadGateway)]
    public async Task Refund_DeveMapearErroParaStatusHttp(string errorCode, int expectedStatusCode)
    {
        FakePaymentService paymentService = new()
        {
            RefundResult = new PaymentResponseDto
            {
                Status = PaymentStatuses.Error,
                ErrorCode = errorCode
            }
        };
        AdminPaymentsController controller = CreateController(paymentService);

        PaymentResponseDto result = await controller.Refund(
            Guid.NewGuid(),
            new PaymentRefundRequestDto { IdempotencyKey = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(PaymentStatuses.Error, result.Status);
        Assert.Equal(expectedStatusCode, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Refund_DeveManterStatus200QuandoEstornoForBemSucedido()
    {
        FakePaymentService paymentService = new()
        {
            RefundResult = new PaymentResponseDto { Status = PaymentStatuses.Refunded }
        };
        AdminPaymentsController controller = CreateController(paymentService);

        await controller.Refund(
            Guid.NewGuid(),
            new PaymentRefundRequestDto { IdempotencyKey = Guid.NewGuid() },
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, controller.Response.StatusCode);
    }

    private static AdminPaymentsController CreateController(IPaymentService paymentService)
        => new(paymentService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FakePaymentService : IPaymentService
    {
        public PaymentResponseDto RefundResult { get; init; } = new() { Status = PaymentStatuses.Pending };

        public Task<PaymentResponseDto> ProcessCardPaymentAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentResponseDto> ProcessPixPaymentAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentResponseDto> GetPaymentOrderAsync(string orderId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentResponseDto> LookupPaymentOrderAsync(string orderId, string email, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentResponseDto> GetPaymentStatusAsync(string nsu, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentReconciliationResponseDto> ReconcileApprovedPaymentsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PagedResult<AdminPaymentResponseDto>> GetAdminPaymentsAsync(PaymentQueryParams query, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<PaymentResponseDto> RefundPaymentAsync(string orderId, decimal? amount, Guid idempotencyKey, CancellationToken cancellationToken)
            => Task.FromResult(RefundResult);

        public Task ProcessApprovedPixPaymentAsync(string mpOrderId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ReconcilePendingPaymentsAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException();

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
            => throw new NotSupportedException();
    }
}
