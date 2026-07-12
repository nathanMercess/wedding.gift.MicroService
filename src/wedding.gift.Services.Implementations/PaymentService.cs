using Microsoft.EntityFrameworkCore;
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
    IEmailService emailService,
    IApplicationCacheService cacheService,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const int CreditCardMaxInstallments = 12;
    private static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(15);

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

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return await BuildErrorResponseAsync("card", "validation", "OrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentResponseDto? existing = await GetExistingOrderResponseAsync(request.OrderId, cancellationToken);
        if (existing is not null)
            return existing;

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("card", "validation", "GiftId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("card", "validation", "ContributorName is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CardToken))
            return await BuildErrorResponseAsync("card", "validation", "CardToken is required.", PaymentErrorCodes.ValidationError, cancellationToken);

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

        PaymentIntentRequest intentRequest = new(
            request.GiftId,
            request.ContributorName,
            request.Message ?? string.Empty,
            request.PayerEmail,
            request.PayerDocType,
            request.PayerDocNumber,
            request.OrderId,
            request.Method,
            request.Amount,
            request.Installments);

        (Payment? payment, PaymentResponseDto? error) = await ReservePaymentIntentAsync(intentRequest, "card", cancellationToken);
        if (error is not null)
            return error;

        PaymentResponseDto providerResult = await mercadoPagoService.CreateCardOrderAsync(request, cancellationToken);
        return await ApplyProviderResultAsync(payment!, providerResult, "card", cancellationToken);
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

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return await BuildErrorResponseAsync("pix", "validation", "OrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentResponseDto? existing = await GetExistingOrderResponseAsync(request.OrderId, cancellationToken);
        if (existing is not null)
            return existing;

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("pix", "validation", "GiftId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("pix", "validation", "ContributorName is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount <= 0)
            return await BuildErrorResponseAsync("pix", "validation", "Invalid amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("pix", "validation", "PayerEmail is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("pix", "validation", "PayerDocNumber (CPF) is required for Pix.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentIntentRequest intentRequest = new(
            request.GiftId,
            request.ContributorName,
            request.Message ?? string.Empty,
            request.PayerEmail,
            request.PayerDocType,
            request.PayerDocNumber,
            request.OrderId,
            "pix",
            request.Amount,
            0);

        (Payment? payment, PaymentResponseDto? error) = await ReservePaymentIntentAsync(intentRequest, "pix", cancellationToken);
        if (error is not null)
            return error;

        PaymentResponseDto providerResult = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);
        return await ApplyProviderResultAsync(payment!, providerResult, "pix", cancellationToken);
    }

    public async Task<PaymentResponseDto> GetPaymentOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return await BuildErrorResponseAsync("order", "validation", "OrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        Payment payment = await paymentRepository.GetByOrderIdAsync(orderId.Trim(), cancellationToken);

        if (payment is null)
            return await BuildErrorResponseAsync("order", "lookup", "Payment order not found.", PaymentErrorCodes.OrderNotFound, cancellationToken);

        return await RefreshAndBuildResponseAsync(payment, cancellationToken);
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return await BuildErrorResponseAsync("status", "validation", "MpOrderId is required.", PaymentErrorCodes.ValidationError, cancellationToken);

        Payment? payment = await paymentRepository.GetByProviderIdAsync(mpOrderId.Trim(), cancellationToken);

        if (payment is not null && PaymentStatuses.IsSettled(payment.Status))
        {
            if (!payment.ContributionCreated)
                await TryCreateContributionForSettledPaymentAsync(payment.MpOrderId, cancellationToken);

            Payment refreshedPayment = await paymentRepository.GetByProviderIdAsync(mpOrderId.Trim(), cancellationToken);
            return ToResponseDto(refreshedPayment ?? payment);
        }

        PaymentResponseDto providerResult = await mercadoPagoService.GetOrderStatusAsync(mpOrderId.Trim(), cancellationToken);

        if (providerResult.Status == PaymentStatuses.Error)
            return await BuildErrorResponseAsync("status", "mercado_pago", providerResult, cancellationToken);

        if (payment is null)
            return providerResult;

        Payment updatedPayment = await UpdatePaymentFromProviderAsync(payment, providerResult, cancellationToken);

        if (PaymentStatuses.IsSettled(updatedPayment.Status))
        {
            await TryCreateContributionForSettledPaymentAsync(updatedPayment.MpOrderId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(updatedPayment.MpOrderId))
                updatedPayment = await paymentRepository.GetByProviderIdAsync(updatedPayment.MpOrderId, cancellationToken) ?? updatedPayment;
        }

        return ToResponseDto(updatedPayment);
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
                bool created = await TryCreateContributionForSettledPaymentAsync(payment.MpOrderId, cancellationToken);
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
        => await TryCreateContributionForSettledPaymentAsync(mpOrderId, cancellationToken);

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        CancellationToken cancellationToken)
        => await ConfirmPaymentAsync(mpOrderId, status, null, null, cancellationToken);

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        string? statusDetail,
        string? mpPaymentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return;

        Payment? payment = await paymentRepository.GetByProviderIdForUpdateAsync(mpOrderId.Trim(), cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Webhook: pagamento com MpOrderId={MpOrderId} nao encontrado para confirmar.", mpOrderId);
            return;
        }

        string normalizedStatus = PaymentStatuses.Normalize(status);

        if (!string.Equals(payment.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payment.StatusDetail, statusDetail, StringComparison.Ordinal))
        {
            payment.UpdateProviderStatus(normalizedStatus, statusDetail, mpOrderId, mpPaymentId);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();
        }

        if (PaymentStatuses.IsSettled(normalizedStatus))
            await TryCreateContributionForSettledPaymentAsync(mpOrderId, cancellationToken);
    }

    private async Task<PaymentResponseDto?> GetExistingOrderResponseAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        Payment? existingPayment = await paymentRepository.GetByOrderIdAsync(orderId.Trim(), cancellationToken);

        return existingPayment is null
            ? null
            : await RefreshAndBuildResponseAsync(existingPayment, cancellationToken);
    }

    private async Task<(Payment? Payment, PaymentResponseDto? Error)> ReservePaymentIntentAsync(
        PaymentIntentRequest request,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        await using IRepositoryTransaction? transaction = await paymentRepository.BeginSerializableTransactionAsync(cancellationToken);

        Payment? existingPayment = await paymentRepository.GetByOrderIdForUpdateAsync(request.OrderId.Trim(), cancellationToken);
        if (existingPayment is not null)
            return (null, ToResponseDto(existingPayment));

        Gift gift = await giftRepository.GetByIdWithContributionsAsync(request.GiftId, cancellationToken);

        if (gift is null)
            return (null, await BuildErrorResponseAsync(paymentMethod, "validation", "Gift not found.", PaymentErrorCodes.ValidationError, cancellationToken));

        PaymentResponseDto? validationError = await ValidateGiftForPaymentAsync(gift, request.Amount, paymentMethod, cancellationToken);
        if (validationError is not null)
            return (null, validationError);

        Payment payment = request.Method == "pix"
            ? Payment.CreatePix(
                request.GiftId,
                gift.Name,
                request.ContributorName,
                request.Message,
                request.PayerEmail,
                request.PayerDocType,
                request.PayerDocNumber,
                request.OrderId,
                request.Amount,
                PaymentStatuses.Pending,
                null,
                null,
                null,
                string.Empty,
                null,
                DateTime.UtcNow.Add(ReservationDuration))
            : Payment.CreateCard(
                request.GiftId,
                gift.Name,
                request.ContributorName,
                request.Message,
                request.PayerEmail,
                request.PayerDocType,
                request.PayerDocNumber,
                null,
                request.OrderId,
                request.Method,
                request.Amount,
                request.Installments,
                PaymentStatuses.Pending,
                null,
                null,
                null,
                DateTime.UtcNow.Add(ReservationDuration));

        await paymentRepository.AddAsync(payment, cancellationToken);
        await paymentRepository.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        cacheService.Invalidate();

        return (payment, null);
    }

    private async Task<PaymentResponseDto> ApplyProviderResultAsync(
        Payment reservedPayment,
        PaymentResponseDto providerResult,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        if (providerResult.Status == PaymentStatuses.Error)
            await BuildErrorResponseAsync(paymentMethod, "mercado_pago", providerResult, cancellationToken);

        Payment payment = await paymentRepository.GetByOrderIdForUpdateAsync(reservedPayment.OrderId, cancellationToken)
                          ?? reservedPayment;

        string normalizedStatus = PaymentStatuses.Normalize(providerResult.Status);
        payment.UpdateProviderStatus(
            normalizedStatus,
            providerResult.StatusDetail,
            providerResult.MpOrderId,
            providerResult.MpPaymentId,
            providerResult.QrCode,
            providerResult.QrCodeBase64);

        await paymentRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        if (PaymentStatuses.IsSettled(normalizedStatus))
        {
            await TryCreateContributionForSettledPaymentAsync(payment.MpOrderId, cancellationToken);

            Payment? refreshedPayment = !string.IsNullOrWhiteSpace(payment.MpOrderId)
                ? await paymentRepository.GetByMpOrderIdAsync(payment.MpOrderId, cancellationToken)
                : await paymentRepository.GetByOrderIdAsync(payment.OrderId, cancellationToken);

            payment = refreshedPayment ?? payment;
        }

        PaymentResponseDto response = ToResponseDto(payment);
        response.ErrorCode = providerResult.ErrorCode;
        response.MpRequestId = providerResult.MpRequestId;

        if (providerResult.Status == PaymentStatuses.Error)
            response.Message = providerResult.Message;

        return response;
    }

    private async Task<PaymentResponseDto> RefreshAndBuildResponseAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        Payment refreshedPayment = payment;

        if (!string.IsNullOrWhiteSpace(refreshedPayment.MpOrderId) &&
            (PaymentStatuses.IsReserving(refreshedPayment.Status) ||
             refreshedPayment.Status == PaymentStatuses.Expired ||
             refreshedPayment.Status == PaymentStatuses.Processed))
        {
            PaymentResponseDto providerResult = await mercadoPagoService.GetOrderStatusAsync(refreshedPayment.MpOrderId, cancellationToken);

            if (providerResult.Status != PaymentStatuses.Error)
            {
                refreshedPayment = await UpdatePaymentFromProviderAsync(refreshedPayment, providerResult, cancellationToken);

                if (PaymentStatuses.IsSettled(refreshedPayment.Status))
                {
                    await TryCreateContributionForSettledPaymentAsync(refreshedPayment.MpOrderId, cancellationToken);
                    refreshedPayment = await paymentRepository.GetByOrderIdAsync(refreshedPayment.OrderId, cancellationToken) ?? refreshedPayment;
                }
            }
        }

        refreshedPayment = await ExpirePaymentIfNeededAsync(refreshedPayment, cancellationToken);

        if (PaymentStatuses.IsSettled(refreshedPayment.Status) && !refreshedPayment.ContributionCreated)
        {
            await TryCreateContributionForSettledPaymentAsync(refreshedPayment.MpOrderId, cancellationToken);
            refreshedPayment = await paymentRepository.GetByOrderIdAsync(refreshedPayment.OrderId, cancellationToken) ?? refreshedPayment;
        }

        return ToResponseDto(refreshedPayment);
    }

    private async Task<Payment> ExpirePaymentIfNeededAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!PaymentStatuses.IsReserving(payment.Status) || payment.ExpiresAt > DateTime.UtcNow)
            return payment;

        Payment? paymentToUpdate = await paymentRepository.GetByOrderIdForUpdateAsync(payment.OrderId, cancellationToken);

        if (paymentToUpdate is null)
            return payment;

        if (PaymentStatuses.IsReserving(paymentToUpdate.Status) && paymentToUpdate.ExpiresAt <= DateTime.UtcNow)
        {
            paymentToUpdate.Expire();
            await paymentRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();
        }

        return await paymentRepository.GetByOrderIdAsync(payment.OrderId, cancellationToken) ?? paymentToUpdate;
    }

    private async Task<Payment> UpdatePaymentFromProviderAsync(
        Payment payment,
        PaymentResponseDto providerResult,
        CancellationToken cancellationToken)
    {
        Payment paymentToUpdate = await paymentRepository.GetByOrderIdForUpdateAsync(payment.OrderId, cancellationToken)
                                  ?? payment;

        paymentToUpdate.UpdateProviderStatus(
            PaymentStatuses.Normalize(providerResult.Status),
            providerResult.StatusDetail,
            providerResult.MpOrderId,
            providerResult.MpPaymentId,
            providerResult.QrCode,
            providerResult.QrCodeBase64);

        await paymentRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
        return await paymentRepository.GetByOrderIdAsync(paymentToUpdate.OrderId, cancellationToken) ?? paymentToUpdate;
    }

    private async Task<PaymentResponseDto?> ValidateGiftForPaymentAsync(
        Gift gift,
        decimal amount,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        bool allowsUnlimitedPurchases = await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken);

        if (!gift.Available && !allowsUnlimitedPurchases)
            return await BuildErrorResponseAsync(paymentMethod, "validation", "Gift is not available.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (allowsUnlimitedPurchases)
            return null;

        decimal remainingAmount = await GetRemainingAmountAsync(gift, cancellationToken);

        if (!gift.AllowPartialContribution && amount < remainingAmount)
            return await BuildErrorResponseAsync(paymentMethod, "validation", "Gift does not allow partial contribution.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (amount > remainingAmount)
            return await BuildInsufficientAmountResponseAsync(paymentMethod, remainingAmount, cancellationToken);

        return null;
    }

    private async Task<decimal> GetRemainingAmountAsync(Gift gift, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        decimal reservedAmount = await paymentRepository.Query()
            .Where(x => x.GiftId == gift.Id &&
                        !x.ContributionCreated &&
                        x.ExpiresAt > now &&
                        PaymentStatuses.Reserving.Contains(x.Status))
            .SumAsync(x => x.Amount, cancellationToken);

        return Math.Max(gift.Total - gift.RaisedAmount - reservedAmount, 0);
    }

    private async Task<bool> TryCreateContributionForSettledPaymentAsync(
        string? mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return false;

        Payment existingPayment = await paymentRepository.GetByProviderIdAsync(mpOrderId, cancellationToken);

        if (existingPayment is null)
        {
            logger.LogError("Pagamento liquidado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada.", mpOrderId);
            return false;
        }

        if (!PaymentStatuses.IsSettled(existingPayment.Status))
        {
            PaymentResponseDto providerStatus = await mercadoPagoService.GetOrderStatusAsync(mpOrderId, cancellationToken);

            if (providerStatus.Status == PaymentStatuses.Error)
                return false;

            Payment paymentToUpdate = await paymentRepository.GetByProviderIdForUpdateAsync(mpOrderId, cancellationToken);

            if (paymentToUpdate is null)
            {
                logger.LogError("Pagamento liquidado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada ao atualizar status.", mpOrderId);
                return false;
            }

            paymentToUpdate.UpdateProviderStatus(
                PaymentStatuses.Normalize(providerStatus.Status),
                providerStatus.StatusDetail,
                providerStatus.MpOrderId,
                providerStatus.MpPaymentId,
                providerStatus.QrCode,
                providerStatus.QrCodeBase64);

            await paymentRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();
            existingPayment = await paymentRepository.GetByProviderIdAsync(mpOrderId, cancellationToken);
        }

        if (existingPayment?.ContributionCreated == true)
            return false;

        if (!PaymentStatuses.IsSettled(existingPayment?.Status))
            return false;

        await using IRepositoryTransaction? transaction = await paymentRepository.BeginSerializableTransactionAsync(cancellationToken);

        Payment payment = await paymentRepository.GetByProviderIdForUpdateAsync(mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogError("Pagamento liquidado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada dentro da transacao.", mpOrderId);
            return false;
        }

        if (payment.ContributionCreated)
            return false;

        if (!PaymentStatuses.IsSettled(payment.Status))
            return false;

        bool giftExists = await giftRepository.ExistsAsync(payment.GiftId, cancellationToken);

        if (!giftExists)
        {
            logger.LogError(
                "Pagamento liquidado com MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}, mas o presente nao foi encontrado.",
                payment.MpOrderId,
                payment.OrderId,
                payment.GiftId);
            return false;
        }

        Contribution contribution = Contribution.Create(
            payment.GiftId,
            payment.ContributorName,
            payment.Message ?? string.Empty,
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
                "Erro ao registrar contribuicao aprovada. MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}.",
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
            Status = PaymentStatuses.Error,
            ErrorCode = errorCode,
            Message = message
        };
    }

    private async Task<PaymentResponseDto> BuildInsufficientAmountResponseAsync(
        string paymentMethod,
        decimal remainingAmount,
        CancellationToken cancellationToken)
    {
        string message = $"Valor solicitado indisponivel. Saldo restante: {remainingAmount:0.00}.";
        await QueuePaymentErrorNotificationAsync(paymentMethod, "validation", message, PaymentErrorCodes.InsufficientAmount, cancellationToken);

        return new PaymentResponseDto
        {
            Status = PaymentStatuses.Error,
            ErrorCode = PaymentErrorCodes.InsufficientAmount,
            Message = message,
            RemainingAmount = remainingAmount
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

    private PaymentResponseDto ToResponseDto(Payment payment)
    {
        bool settled = PaymentStatuses.IsSettled(payment.Status);

        return new PaymentResponseDto
        {
            OrderId = payment.OrderId,
            GiftId = payment.GiftId,
            GiftName = payment.GiftName,
            Amount = payment.Amount,
            ContributorName = payment.ContributorName,
            Method = payment.Method,
            Status = payment.Status,
            StatusDetail = payment.StatusDetail,
            Message = payment.Message ?? string.Empty,
            MpOrderId = payment.MpOrderId,
            MpPaymentId = payment.MpPaymentId,
            ContributionCreated = payment.ContributionCreated,
            PaidAt = settled ? payment.Contribution?.PaidAt ?? payment.UpdatedAt : null,
            CreatedAt = payment.CreatedAt,
            UpdatedAt = payment.UpdatedAt,
            ExpiresAt = payment.ExpiresAt,
            QrCode = payment.PixQrCode ?? string.Empty,
            QrCodeBase64 = payment.QrCodeBase64
        };
    }

    private static string NormalizeValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private async Task<bool> CoupleAllowsUnlimitedPurchasesAsync(CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        return GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);
    }

    private sealed record PaymentIntentRequest(
        Guid GiftId,
        string ContributorName,
        string Message,
        string PayerEmail,
        string PayerDocType,
        string PayerDocNumber,
        string OrderId,
        string Method,
        decimal Amount,
        int Installments);
}
