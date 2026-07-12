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
    ICoupleRepository coupleRepository,
    IContributionService contributionService,
    IEmailService emailService,
    IApplicationCacheService cacheService,
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

        if (!await CanGiftReceivePaymentAsync(gift, cancellationToken))
            return await BuildErrorResponseAsync("card", "validation", "Gift is not available.", PaymentErrorCodes.ValidationError, cancellationToken);

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

            try
            {
                await emailService.SendContributionNotificationAsync(request.ContributorName, request.Amount, cancellationToken);
            }
            catch (EmailDeliveryException ex)
            {
                logger.LogError(ex, "Contribution {ContributionId} confirmed, but email notification failed.", contributionId);
            }
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
        cacheService.Invalidate();

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

        Gift gift = await giftRepository.GetByIdAsync(request.GiftId, cancellationToken);

        if (gift is null)
            return await BuildErrorResponseAsync("pix", "validation", "Gift not found.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (!await CanGiftReceivePaymentAsync(gift, cancellationToken))
            return await BuildErrorResponseAsync("pix", "validation", "Gift is not available.", PaymentErrorCodes.ValidationError, cancellationToken);

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
        cacheService.Invalidate();

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
        {
            await paymentRepository.UpdateStatusAsync(payment.OrderId, result.Status, result.StatusDetail, cancellationToken);
            cacheService.Invalidate();
        }

        if (result.Status == "approved")
        {
            await ProcessApprovedPixPaymentAsync(mpOrderId, cancellationToken);

            Payment processedPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);
            result.ContributionCreated = processedPayment?.ContributionCreated;
        }

        return result;
    }

    public async Task<PaymentReconciliationResponseDto> ReconcileApprovedPaymentsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Payment> payments = await paymentRepository.GetApprovedWithoutContributionAsync(cancellationToken);
        PaymentReconciliationResponseDto response = new()
        {
            CheckedCount = payments.Count
        };

        foreach (Payment payment in payments)
        {
            PaymentReconciliationItemDto item = new()
            {
                MpOrderId = payment.MpOrderId ?? string.Empty,
                OrderId = payment.OrderId,
                Method = payment.Method,
                Status = payment.Status,
                ContributionCreated = payment.ContributionCreated
            };

            if (string.IsNullOrWhiteSpace(payment.MpOrderId))
            {
                item.Result = "missing_mp_order_id";
                response.SkippedCount++;
                response.Items.Add(item);
                continue;
            }

            try
            {
                bool created = await TryCreateContributionForApprovedPaymentAsync(payment.MpOrderId, cancellationToken);
                Payment? updatedPayment = await paymentRepository.GetByMpOrderIdAsync(payment.MpOrderId, cancellationToken);

                item.ContributionCreated = updatedPayment?.ContributionCreated ?? false;
                item.Result = created ? "created" : item.ContributionCreated ? "already_created" : "not_created";

                if (created)
                    response.CreatedCount++;
                else
                    response.SkippedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Falha ao reconciliar pagamento aprovado. MpOrderId={MpOrderId}, OrderId={OrderId}.",
                    payment.MpOrderId,
                    payment.OrderId);

                item.Result = "failed";
                response.FailedCount++;
            }

            response.Items.Add(item);
        }

        return response;
    }

    public async Task ProcessApprovedPixPaymentAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
        => await TryCreateContributionForApprovedPaymentAsync(mpOrderId, cancellationToken);

    private async Task<bool> TryCreateContributionForApprovedPaymentAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return false;

        Payment existingPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);

        if (existingPayment is null)
        {
            logger.LogError("Pagamento aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada.", mpOrderId);
            return false;
        }

        if (!string.Equals(existingPayment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            PaymentResponseDto providerStatus = await mercadoPagoService.GetOrderStatusAsync(mpOrderId, cancellationToken);

            if (providerStatus.Status == "error")
                return false;

            Payment paymentToUpdate = await paymentRepository.GetByMpOrderIdForUpdateAsync(mpOrderId, cancellationToken);

            if (paymentToUpdate is null)
            {
                logger.LogError("Pagamento aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada ao atualizar status.", mpOrderId);
                return false;
            }

            paymentToUpdate.UpdateProviderStatus(providerStatus.Status, providerStatus.StatusDetail, providerStatus.MpPaymentId);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();

            existingPayment = await paymentRepository.GetByMpOrderIdAsync(mpOrderId, cancellationToken);
        }

        if (existingPayment?.ContributionCreated == true)
            return false;

        if (!string.Equals(existingPayment?.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return false;

        await using IRepositoryTransaction transaction = await paymentRepository.BeginSerializableTransactionAsync(cancellationToken);

        Payment payment = await paymentRepository.GetByMpOrderIdForUpdateAsync(mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogError("Pagamento aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada dentro da transacao.", mpOrderId);
            return false;
        }

        if (payment.ContributionCreated)
            return false;

        if (!string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return false;

        bool giftExists = await giftRepository.ExistsAsync(payment.GiftId, cancellationToken);

        if (!giftExists)
        {
            logger.LogError(
                "Pagamento aprovado com MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}, mas o presente nao foi encontrado.",
                payment.MpOrderId,
                payment.OrderId,
                payment.GiftId);
            return false;
        }

        Contribution contribution = Contribution.Create(
            payment.GiftId,
            payment.ContributorName,
            payment.Message,
            payment.Amount,
            payment.Method,
            DateTime.UtcNow,
            ContributionStatus.Paid);

        await contributionRepository.AddAsync(contribution, cancellationToken);
        payment.MarkContributionCreated(contribution.Id);

        try
        {
            await paymentRepository.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            cacheService.Invalidate();
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

        return true;
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
            cacheService.Invalidate();
        }

        if (status == "approved")
            await ProcessApprovedPixPaymentAsync(mpOrderId, cancellationToken);
    }

    private async Task QueuePaymentAttemptNotificationAsync(
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

        try
        {
            await emailService.SendPaymentAttemptNotificationAsync(subject, body, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger.LogError(ex, "Payment attempt notification failed. PaymentMethod={PaymentMethod}, OrderId={OrderId}.", paymentMethod, orderId);
        }
    }

    private async Task QueuePaymentErrorNotificationAsync(
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

        try
        {
            await emailService.SendErrorNotificationAsync(subject, body, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger.LogError(ex, "Payment error notification failed. PaymentMethod={PaymentMethod}, Stage={Stage}.", paymentMethod, stage);
        }
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

    private async Task<bool> CanGiftReceivePaymentAsync(Gift gift, CancellationToken cancellationToken)
    {
        if (gift.Available)
            return true;

        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        return GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);
    }
}
