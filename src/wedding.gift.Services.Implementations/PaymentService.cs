using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations;

public class PaymentService(
    IMercadoPagoService mercadoPagoService,
    IPaymentRepository paymentRepository,
    IContributionService contributionService,
    IEmailService emailService,
    IBackgroundTaskQueue backgroundTaskQueue,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponseDto> ProcessCardPaymentAsync(
        CardPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        await QueuePaymentAttemptNotificationAsync(
            "card",
            request.ContributorName,
            request.GiftId,
            request.Amount,
            request.OrderId,
            request.PayerEmail,
            cancellationToken);

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("card", "validation", "GiftId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("card", "validation", "ContributorName is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CardToken))
            return await BuildErrorResponseAsync("card", "validation", "CardToken is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return await BuildErrorResponseAsync("card", "validation", "OrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount <= 0)
            return await BuildErrorResponseAsync("card", "validation", "Invalid amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Installments <= 0)
            return await BuildErrorResponseAsync("card", "validation", "Invalid installments.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Method != "credit_card" && request.Method != "debit_card")
            return await BuildErrorResponseAsync("card", "validation", "Invalid method.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return await BuildErrorResponseAsync("card", "validation", "PaymentMethodId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("card", "validation", "PayerEmail is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("card", "validation", "PayerDocNumber is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        var result = await mercadoPagoService.CreateCardOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("card", "mercado_pago", result, cancellationToken);

        Guid? contributionId = null;

        if (result.Status == "approved")
        {
            var contribution = await contributionService.CreateAsync(new ContributionCreateDto
            {
                GiftId = request.GiftId,
                ContributorName = request.ContributorName,
                Amount = request.Amount,
                PaymentMethod = request.Method,
                Status = ContributionStatus.Paid,
                PaidAt = DateTime.UtcNow
            }, cancellationToken);

            contributionId = contribution.Id;

            var contributorName = request.ContributorName;
            var amount = request.Amount;
            await backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
            {
                var email = sp.GetRequiredService<IEmailService>();
                await email.SendContributionNotificationAsync(contributorName, amount, ct);
            });
        }

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = request.GiftId,
            ContributorName = request.ContributorName,
            ContributionId = contributionId,
            OrderId = request.OrderId,
            Method = request.Method,
            Amount = request.Amount,
            Installments = request.Installments,
            Status = result.Status,
            StatusDetail = result.StatusDetail,
            MpOrderId = result.MpOrderId,
            MpPaymentId = result.MpPaymentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> ProcessPixPaymentAsync(
        PixPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        await QueuePaymentAttemptNotificationAsync(
            "pix",
            request.ContributorName,
            request.GiftId,
            request.Amount,
            request.OrderId,
            request.PayerEmail,
            cancellationToken);

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("pix", "validation", "GiftId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("pix", "validation", "ContributorName is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return await BuildErrorResponseAsync("pix", "validation", "OrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount <= 0)
            return await BuildErrorResponseAsync("pix", "validation", "Invalid amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("pix", "validation", "PayerEmail is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("pix", "validation", "PayerDocNumber (CPF) is required for Pix.", PaymentErrorCodes.ValidationError, cancellationToken);

        var result = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("pix", "mercado_pago", result, cancellationToken);

        var contribution = await contributionService.CreateAsync(new ContributionCreateDto
        {
            GiftId = request.GiftId,
            ContributorName = request.ContributorName,
            Amount = request.Amount,
            PaymentMethod = "pix",
            Status = ContributionStatus.Pending,
            PaidAt = default
        }, cancellationToken);

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = request.GiftId,
            ContributorName = request.ContributorName,
            ContributionId = contribution.Id,
            OrderId = request.OrderId,
            Method = "pix",
            Amount = request.Amount,
            Status = result.Status,
            StatusDetail = result.StatusDetail,
            MpOrderId = result.MpOrderId,
            MpPaymentId = result.MpPaymentId,
            PixQrCode = result.QrCode,
            QrCodeBase64 = result.QrCodeBase64,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return await BuildErrorResponseAsync("status", "validation", "MpOrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        var payment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

        if (payment?.Status == "approved")
            return new PaymentResponseDto
            {
                Status = "approved",
                MpOrderId = mpOrderId,
                StatusDetail = payment.StatusDetail
            };

        var result = await mercadoPagoService.GetOrderStatusAsync(mpOrderId, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("status", "mercado_pago", result, cancellationToken);

        if (payment != null && payment.Status != result.Status)
            await paymentRepository.UpdateStatusAsync(payment.OrderId, result.Status, result.StatusDetail, cancellationToken);

        return result;
    }

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return;

        var payment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Webhook: pagamento com MpOrderId={MpOrderId} nao encontrado para confirmar.", mpOrderId);
            return;
        }

        if (string.Equals(payment.Status, status, StringComparison.OrdinalIgnoreCase))
            return;

        await paymentRepository.UpdateStatusAsync(payment.OrderId, status, payment.StatusDetail, cancellationToken);

        if (status == "approved" && payment.ContributionId.HasValue)
        {
            await contributionService.UpdateStatusAsync(
                payment.ContributionId.Value, ContributionStatus.Paid, DateTime.UtcNow, cancellationToken);

            try
            {
                await emailService.SendContributionNotificationAsync(payment.ContributorName, payment.Amount, cancellationToken);
            }
            catch (EmailDeliveryException ex)
            {
                logger.LogError(ex, "Contribution {ContributionId} confirmed, but email notification failed.", payment.ContributionId);
            }
        }
    }

    private ValueTask QueuePaymentAttemptNotificationAsync(
        string paymentMethod,
        string? contributorName,
        Guid giftId,
        decimal amount,
        string? orderId,
        string? payerEmail,
        CancellationToken cancellationToken)
    {
        var subject = $"[wedding.gift] Payment attempt ({paymentMethod})";
        var body = $"""
            Payment attempt received.

            Method: {paymentMethod}
            Contributor: {NormalizeValue(contributorName)}
            GiftId: {giftId}
            Amount: {amount}
            OrderId: {NormalizeValue(orderId)}
            PayerEmail: {NormalizeValue(payerEmail)}
            UTC time: {DateTime.UtcNow:u}
            """;

        return backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
        {
            var email = sp.GetRequiredService<IEmailService>();
            await email.SendPaymentAttemptNotificationAsync(subject, body, ct);
        });
    }

    private ValueTask QueuePaymentErrorNotificationAsync(
        string paymentMethod,
        string stage,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        var subject = $"[wedding.gift] Payment error ({paymentMethod})";
        var body = $"""
            Payment flow failure.

            Method: {paymentMethod}
            Stage: {stage}
            Message: {message}
            Details: {NormalizeValue(details)}
            UTC time: {DateTime.UtcNow:u}
            """;

        return backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
        {
            var email = sp.GetRequiredService<IEmailService>();
            await email.SendErrorNotificationAsync(subject, body, ct);
        });
    }

    private async Task<PaymentResponseDto> BuildErrorResponseAsync(
        string paymentMethod,
        string stage,
        string message,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        await QueuePaymentErrorNotificationAsync(paymentMethod, stage, message, errorCode, cancellationToken);

        return new PaymentResponseDto
        {
            Status = "error",
            ErrorCode = errorCode,
            Message = message
        };
    }

    private async Task<PaymentResponseDto> BuildErrorResponseAsync(
        string paymentMethod,
        string stage,
        PaymentResponseDto result,
        CancellationToken cancellationToken)
    {
        await QueuePaymentErrorNotificationAsync(
            paymentMethod,
            stage,
            result.Message,
            $"Status: {result.Status}\nErrorCode: {result.ErrorCode}\nStatusDetail: {result.StatusDetail}\nMpOrderId: {result.MpOrderId}\nMpPaymentId: {result.MpPaymentId}\nMpRequestId: {result.MpRequestId}",
            cancellationToken);

        return result;
    }

    private static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
}
