using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations;

public sealed class PaymentService(
    IMercadoPagoService mercadoPagoService,
    IPaymentRepository paymentRepository,
    IGiftRepository giftRepository,
    IContributionRepository contributionRepository,
    IContributionService contributionService,
    IEmailService emailService,
    IBackgroundTaskQueue backgroundTaskQueue,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const int CreditCardMaxInstallments = 12;

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

        if (request.Installments > CreditCardMaxInstallments)
            return await BuildErrorResponseAsync("card", "validation", "Installments exceed the card limit.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Method != "credit_card" && request.Method != "debit_card")
            return await BuildErrorResponseAsync("card", "validation", "Invalid method.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return await BuildErrorResponseAsync("card", "validation", "PaymentMethodId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("card", "validation", "PayerEmail is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("card", "validation", "PayerDocNumber is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        Gift gift = await giftRepository.GetByIdWithContributionsAsync(request.GiftId, cancellationToken);

        if (gift is null)
            return await BuildErrorResponseAsync("card", "validation", "Gift not found.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (!gift.Available)
            return await BuildErrorResponseAsync("card", "validation", "Gift is not available.", PaymentErrorCodes.ValidationError, cancellationToken);

        decimal raised = gift.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .Sum(x => x.Amount);
        decimal remainingAmount = gift.Total - raised;

        if (request.Amount > remainingAmount)
            return await BuildErrorResponseAsync("card", "validation", "Amount exceeds remaining gift amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentResponseDto result = await mercadoPagoService.CreateCardOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("card", "mercado_pago", result, cancellationToken);

        Guid? contributionId = null;

        if (result.Status == "approved")
        {
            ContributionResponseDto contribution = await contributionService.CreateAsync(new ContributionCreateDto
            {
                GiftId = request.GiftId,
                ContributorName = request.ContributorName,
                Message = request.Message?.Trim() ?? string.Empty,
                Amount = request.Amount,
                PaymentMethod = request.Method,
                Status = ContributionStatus.Paid,
                PaidAt = DateTime.UtcNow
            }, cancellationToken);

            contributionId = contribution.Id;

            string contributorName = request.ContributorName;
            decimal amount = request.Amount;
            await backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
            {
                IEmailService email = sp.GetRequiredService<IEmailService>();
                await email.SendContributionNotificationAsync(contributorName, amount, ct);
            }, cancellationToken);
        }

        Payment payment = Payment.CreateCard(
            request.GiftId,
            request.ContributorName,
            request.Message ?? string.Empty,
            request.PayerEmail,
            request.PayerDocType,
            request.PayerDocNumber,
            contributionId,
            request.OrderId,
            request.Method,
            request.Amount,
            request.Installments,
            result.Status,
            result.StatusDetail,
            result.MpOrderId,
            result.MpPaymentId);

        await paymentRepository.SaveAsync(payment, cancellationToken);

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

        PaymentResponseDto result = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("pix", "mercado_pago", result, cancellationToken);

        Payment payment = Payment.CreatePix(
            request.GiftId,
            request.ContributorName,
            request.Message ?? string.Empty,
            request.PayerEmail,
            request.PayerDocType,
            request.PayerDocNumber,
            request.OrderId,
            request.Amount,
            result.Status,
            result.StatusDetail,
            result.MpOrderId,
            result.MpPaymentId,
            result.QrCode,
            result.QrCodeBase64);

        await paymentRepository.SaveAsync(payment, cancellationToken);

        if (result.Status == "approved" && !string.IsNullOrWhiteSpace(result.MpOrderId))
            await ProcessApprovedPixPaymentAsync(result.MpOrderId, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return await BuildErrorResponseAsync("status", "validation", "MpOrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        Payment payment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

        if (payment?.Status == "approved")
        {
            if (!payment.ContributionCreated)
                await ProcessApprovedPixPaymentAsync(mpOrderId, cancellationToken);

            payment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

            return new PaymentResponseDto
            {
                Status = "approved",
                MpOrderId = mpOrderId,
                StatusDetail = payment?.StatusDetail,
                ContributionCreated = payment?.ContributionCreated
            };
        }

        PaymentResponseDto result = await mercadoPagoService.GetOrderStatusAsync(mpOrderId, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("status", "mercado_pago", result, cancellationToken);

        if (payment != null && payment.Status != result.Status)
            await paymentRepository.UpdateStatusAsync(payment.OrderId, result.Status, result.StatusDetail, cancellationToken);

        if (result.Status == "approved")
        {
            await ProcessApprovedPixPaymentAsync(mpOrderId, cancellationToken);

            Payment processedPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);
            result.ContributionCreated = processedPayment?.ContributionCreated;
        }

        return result;
    }

    public async Task ProcessApprovedPixPaymentAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return;

        Payment existingPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

        if (existingPayment is null)
        {
            logger.LogError("Pix aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada.", mpOrderId);
            return;
        }

        if (existingPayment.Method != "pix")
            return;

        if (!string.Equals(existingPayment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            PaymentResponseDto providerStatus = await mercadoPagoService.GetOrderStatusAsync(mpOrderId, cancellationToken);

            if (providerStatus.Status == "error")
                return;

            Payment paymentToUpdate = await paymentRepository.GetByMpOrderIdForUpdateAsync(mpOrderId, cancellationToken);

            if (paymentToUpdate is null)
            {
                logger.LogError("Pix aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada ao atualizar status.", mpOrderId);
                return;
            }

            paymentToUpdate.UpdateProviderStatus(providerStatus.Status, providerStatus.StatusDetail, providerStatus.MpPaymentId);
            await paymentRepository.SaveChangesAsync(cancellationToken);

            existingPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);
        }

        if (existingPayment?.ContributionCreated == true)
            return;

        if (!string.Equals(existingPayment?.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return;

        await using IRepositoryTransaction transaction = await paymentRepository.BeginSerializableTransactionAsync(cancellationToken);

        Payment payment = await paymentRepository.GetByMpOrderIdForUpdateAsync(mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogError("Pix aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada dentro da transacao.", mpOrderId);
            return;
        }

        if (payment.ContributionCreated)
            return;

        if (!string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return;

        bool giftExists = await giftRepository.ExistsAsync(payment.GiftId, cancellationToken);

        if (!giftExists)
        {
            logger.LogError(
                "Pix aprovado com MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}, mas o presente nao foi encontrado.",
                payment.MpOrderId,
                payment.OrderId,
                payment.GiftId);
            return;
        }

        Contribution contribution = Contribution.Create(
            payment.GiftId,
            payment.ContributorName,
            payment.Message,
            payment.Amount,
            "pix",
            DateTime.UtcNow,
            ContributionStatus.Paid);

        await contributionRepository.AddAsync(contribution, cancellationToken);
        payment.MarkContributionCreated(contribution.Id);

        try
        {
            await paymentRepository.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Erro ao registrar contribuicao Pix aprovada. MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}.",
                payment.MpOrderId,
                payment.OrderId,
                payment.GiftId);
            throw;
        }

        try
        {
            await emailService.SendContributionNotificationAsync(payment.ContributorName, payment.Amount, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger.LogError(ex, "Contribution {ContributionId} confirmed, but email notification failed.", payment.ContributionId);
        }
    }

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return;

        Payment payment = await paymentRepository.GetByMpOrderIdForUpdateAsync(mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Webhook: pagamento com MpOrderId={MpOrderId} nao encontrado para confirmar.", mpOrderId);
            return;
        }

        if (!string.Equals(payment.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            payment.UpdateProviderStatus(status, payment.StatusDetail);
            await paymentRepository.SaveChangesAsync(cancellationToken);
        }

        if (status == "approved")
            await ProcessApprovedPixPaymentAsync(mpOrderId, cancellationToken);
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
        string subject = $"[wedding.gift] Payment attempt ({paymentMethod})";
        string body = $"""
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
            IEmailService email = sp.GetRequiredService<IEmailService>();
            await email.SendPaymentAttemptNotificationAsync(subject, body, ct);
        }, cancellationToken);
    }

    private ValueTask QueuePaymentErrorNotificationAsync(
        string paymentMethod,
        string stage,
        string message,
        string? details,
        CancellationToken cancellationToken)
    {
        string subject = $"[wedding.gift] Payment error ({paymentMethod})";
        string body = $"""
            Payment flow failure.

            Method: {paymentMethod}
            Stage: {stage}
            Message: {message}
            Details: {NormalizeValue(details)}
            UTC time: {DateTime.UtcNow:u}
            """;

        return backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
        {
            IEmailService email = sp.GetRequiredService<IEmailService>();
            await email.SendErrorNotificationAsync(subject, body, ct);
        }, cancellationToken);
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
