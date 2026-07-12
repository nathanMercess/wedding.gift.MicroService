using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed class PaymentService(
    IMercadoPagoService mercadoPagoService,
    IPaymentRepository paymentRepository,
    IGiftRepository giftRepository,
    IContributionRepository contributionRepository,
    ICoupleRepository coupleRepository,
    IEmailService emailService,
    IApplicationCacheService cacheService,
    ILogger<PaymentService> logger,
    IBackgroundTaskQueue? backgroundTaskQueue = null,
    IRequestContext? requestContext = null,
    IOperationalRepository? operationalRepository = null) : IPaymentService
{
    private const int CreditCardMaxInstallments = 12;
    private static readonly TimeSpan ReservationDuration = TimeSpan.FromMinutes(35);

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

        if (!Guid.TryParse(request.OrderId, out _))
            return await BuildErrorResponseAsync("card", "validation", "O identificador do pedido é inválido.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentResponseDto? existing = await GetExistingOrderResponseAsync(
            request.OrderId,
            request.GiftId,
            request.Amount,
            request.Method,
            request.PayerEmail,
            cancellationToken);
        if (existing is not null)
            return existing;

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("card", "validation", "O presente é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("card", "validation", "O nome do contribuinte é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.CardToken))
            return await BuildErrorResponseAsync("card", "validation", "O token do cartão é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount <= 0)
            return await BuildErrorResponseAsync("card", "validation", "O valor do pagamento é inválido.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Installments <= 0)
            return await BuildErrorResponseAsync("card", "validation", "A quantidade de parcelas é inválida.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Installments > CreditCardMaxInstallments)
            return await BuildErrorResponseAsync("card", "validation", "A quantidade de parcelas excede o limite permitido.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Method != "credit_card" && request.Method != "debit_card")
            return await BuildErrorResponseAsync("card", "validation", "O método de pagamento é inválido.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return await BuildErrorResponseAsync("card", "validation", "A bandeira ou método do cartão é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("card", "validation", "O e-mail do pagador é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("card", "validation", "O documento do pagador é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

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

        if (!Guid.TryParse(request.OrderId, out _))
            return await BuildErrorResponseAsync("pix", "validation", "O identificador do pedido é inválido.", PaymentErrorCodes.ValidationError, cancellationToken);

        PaymentResponseDto? existing = await GetExistingOrderResponseAsync(
            request.OrderId,
            request.GiftId,
            request.Amount,
            "pix",
            request.PayerEmail,
            cancellationToken);
        if (existing is not null)
            return existing;

        if (request.GiftId == Guid.Empty)
            return await BuildErrorResponseAsync("pix", "validation", "O presente é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return await BuildErrorResponseAsync("pix", "validation", "O nome do contribuinte é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount <= 0)
            return await BuildErrorResponseAsync("pix", "validation", "O valor do pagamento é inválido.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return await BuildErrorResponseAsync("pix", "validation", "O e-mail do pagador é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return await BuildErrorResponseAsync("pix", "validation", "O documento do pagador é obrigatório para Pix.", PaymentErrorCodes.ValidationError, cancellationToken);

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
            return await BuildErrorResponseAsync("order", "validation", "O identificador do pedido é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

        Payment payment = await paymentRepository.GetByOrderIdAsync(orderId.Trim(), cancellationToken);

        if (payment is null)
            return await BuildErrorResponseAsync("order", "lookup", "O pedido de pagamento não foi encontrado.", PaymentErrorCodes.OrderNotFound, cancellationToken);

        return await RefreshAndBuildResponseAsync(payment, cancellationToken);
    }

    public async Task<PaymentResponseDto> LookupPaymentOrderAsync(string orderId, string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(email))
            return await BuildErrorResponseAsync("order", "validation", "Informe o pedido e o e-mail utilizado no pagamento.", PaymentErrorCodes.ValidationError, cancellationToken);

        Payment payment = await paymentRepository.GetByOrderIdAsync(orderId.Trim(), cancellationToken);

        if (payment is null || !string.Equals(payment.PayerEmail, email.Trim(), StringComparison.OrdinalIgnoreCase))
            return await BuildErrorResponseAsync("order", "lookup", "Não foi possível localizar um pedido com os dados informados.", PaymentErrorCodes.OrderNotFound, cancellationToken);

        return await RefreshAndBuildResponseAsync(payment, cancellationToken);
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return await BuildErrorResponseAsync("status", "validation", "O identificador do provedor é obrigatório.", PaymentErrorCodes.ValidationError, cancellationToken);

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

        if (operationalRepository is not null)
        {
            await operationalRepository.AddAuditLogAsync(
                AuditLog.Create(requestContext?.UserId, requestContext?.CoupleId, "PaymentsReconciled", "Payment", string.Empty, requestContext?.CorrelationId ?? string.Empty),
                cancellationToken);
            await operationalRepository.SaveChangesAsync(cancellationToken);
        }

        return response;
    }

    public async Task<PagedResult<AdminPaymentResponseDto>> GetAdminPaymentsAsync(
        PaymentQueryParams queryParams,
        CancellationToken cancellationToken)
    {
        IQueryable<Payment> query = paymentRepository.Query();
        Guid? coupleId = requestContext?.IsSuperAdmin == true ? null : requestContext?.CoupleId ?? Couple.SingletonId;
        if (coupleId.HasValue)
            query = query.Where(x => x.CoupleId == coupleId.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            string search = queryParams.Search.Trim();
            query = query.Where(x => x.OrderId.Contains(search) ||
                                     x.GiftName.Contains(search) ||
                                     x.ContributorName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            string status = queryParams.Status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Method))
        {
            string method = queryParams.Method.Trim() switch
            {
                "Pix" => "pix",
                "CreditCard" => "credit_card",
                "DebitCard" => "debit_card",
                _ => queryParams.Method.Trim().ToLowerInvariant()
            };
            query = query.Where(x => x.Method == method);
        }

        if (queryParams.FromUtc.HasValue && queryParams.ToUtc.HasValue && queryParams.ToUtc < queryParams.FromUtc)
            throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);

        if (queryParams.FromUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= queryParams.FromUtc.Value);

        if (queryParams.ToUtc.HasValue)
            query = query.Where(x => x.CreatedAt <= queryParams.ToUtc.Value);

        int totalCount = await query.CountAsync(cancellationToken);
        IQueryable<Payment> orderedQuery = string.Equals(queryParams.OrderDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderBy(x => x.UpdatedAt)
            : query.OrderByDescending(x => x.UpdatedAt);
        List<Payment> payments = await orderedQuery
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminPaymentResponseDto>
        {
            Items = payments.Select(ToAdminResponseDto).ToList(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<PaymentResponseDto> RefundPaymentAsync(string orderId, CancellationToken cancellationToken)
        => await RefundPaymentAsync(orderId, null, cancellationToken);

    public async Task<PaymentResponseDto> RefundPaymentAsync(
        string orderId,
        decimal? amount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new BadRequestException(PaymentErrorCodes.ValidationError);

        Payment payment = await paymentRepository.GetByOrderIdForUpdateAsync(orderId.Trim(), cancellationToken)
                          ?? throw new NotFoundException(PaymentErrorCodes.OrderNotFound);

        if (payment.Status == PaymentStatuses.Refunded && !amount.HasValue)
            return ToResponseDto(payment);

        if (!PaymentStatuses.IsSettled(payment.Status) && payment.Status != PaymentStatuses.PartiallyRefunded)
            throw new ConflictException(PaymentErrorCodes.PaymentNotRefundable);

        decimal refundableAmount = payment.Amount - payment.RefundedAmount;
        decimal amountToRefund = amount ?? refundableAmount;

        if (amountToRefund <= 0 || amountToRefund > refundableAmount)
            throw new ConflictException(PaymentErrorCodes.InvalidRefundAmount);

        PaymentResponseDto providerResult = await mercadoPagoService.RefundAsync(
            payment.MpOrderId,
            payment.MpPaymentId,
            amount,
            $"refund:{payment.Id}:{amountToRefund:0.00}",
            cancellationToken);

        if (providerResult.Status == PaymentStatuses.Error)
            return providerResult;

        decimal refundedAmount = payment.RefundedAmount + amountToRefund;
        string refundStatus = refundedAmount >= payment.Amount
            ? PaymentStatuses.Refunded
            : PaymentStatuses.PartiallyRefunded;
        payment.UpdateProviderStatus(
            refundStatus,
            refundStatus,
            payment.MpOrderId,
            payment.MpPaymentId,
            refundedAmount: refundedAmount);
        await paymentRepository.SaveChangesAsync(cancellationToken);
        await SynchronizeContributionStatusAsync(payment, cancellationToken);
        cacheService.Invalidate();
        return ToResponseDto(payment);
    }

    public async Task ProcessApprovedPixPaymentAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
        => await TryCreateContributionForSettledPaymentAsync(mpOrderId, cancellationToken);

    public async Task ReconcilePendingPaymentsAsync(CancellationToken cancellationToken)
    {
        List<string> providerIds = await paymentRepository.Query()
            .Where(x => PaymentStatuses.Reserving.Contains(x.Status) &&
                        (x.MpOrderId != null || x.MpPaymentId != null))
            .OrderBy(x => x.UpdatedAt)
            .Take(100)
            .Select(x => x.MpOrderId ?? x.MpPaymentId!)
            .ToListAsync(cancellationToken);

        foreach (string providerId in providerIds)
        {
            try
            {
                await GetPaymentStatusAsync(providerId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao reconciliar pagamento pendente. ProviderId={ProviderId}.", providerId);
            }
        }
    }

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        CancellationToken cancellationToken)
        => await ConfirmPaymentAsync(mpOrderId, status, null, null, null, cancellationToken);

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        string? statusDetail,
        string? mpPaymentId,
        CancellationToken cancellationToken)
        => await ConfirmPaymentAsync(mpOrderId, status, statusDetail, mpPaymentId, null, cancellationToken);

    public async Task ConfirmPaymentAsync(
        string mpOrderId,
        string status,
        string? statusDetail,
        string? mpPaymentId,
        decimal? refundedAmount,
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

        string normalizedStatus = PaymentStatuses.Normalize(status, statusDetail);

        if (!string.Equals(payment.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payment.StatusDetail, statusDetail, StringComparison.Ordinal) ||
            refundedAmount.HasValue && payment.RefundedAmount != refundedAmount.Value)
        {
            bool providerIdIsOrder = mpOrderId.StartsWith("ORD", StringComparison.OrdinalIgnoreCase);
            string? resolvedOrderId = providerIdIsOrder ? mpOrderId : null;
            string? resolvedPaymentId = mpPaymentId ?? (providerIdIsOrder ? null : mpOrderId);
            payment.UpdateProviderStatus(
                normalizedStatus,
                statusDetail,
                resolvedOrderId,
                resolvedPaymentId,
                refundedAmount: refundedAmount);
            await paymentRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();
        }

        if (PaymentStatuses.IsSettled(normalizedStatus))
            await TryCreateContributionForSettledPaymentAsync(mpOrderId, cancellationToken);

        Payment refreshedPayment = await paymentRepository.GetByProviderIdAsync(mpOrderId, cancellationToken) ?? payment;
        await SynchronizeContributionStatusAsync(refreshedPayment, cancellationToken);
    }

    private async Task<PaymentResponseDto?> GetExistingOrderResponseAsync(
        string orderId,
        Guid giftId,
        decimal amount,
        string method,
        string payerEmail,
        CancellationToken cancellationToken)
    {
        Payment? existingPayment = await paymentRepository.GetByOrderIdAsync(orderId.Trim(), cancellationToken);

        if (existingPayment is null)
            return null;

        bool sameRequest = existingPayment.GiftId == giftId &&
                           existingPayment.Amount == amount &&
                           string.Equals(existingPayment.Method, method, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(existingPayment.PayerEmail, payerEmail.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!sameRequest)
        {
            return await BuildErrorResponseAsync(
                method,
                "idempotency",
                "O identificador do pedido já foi utilizado com dados diferentes.",
                PaymentErrorCodes.DuplicateOrder,
                cancellationToken);
        }

        return await RefreshAndBuildResponseAsync(existingPayment, cancellationToken);
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
            return (null, await BuildErrorResponseAsync(paymentMethod, "validation", "O presente não foi encontrado.", PaymentErrorCodes.ValidationError, cancellationToken));

        Guid allowedCoupleId = requestContext?.CoupleId ?? Couple.SingletonId;
        if (requestContext?.IsSuperAdmin != true && gift.CoupleId != allowedCoupleId)
            return (null, await BuildErrorResponseAsync(paymentMethod, "validation", "O presente não foi encontrado.", PaymentErrorCodes.ValidationError, cancellationToken));

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
                DateTime.UtcNow.Add(ReservationDuration),
                gift.CoupleId,
                requestContext?.CorrelationId)
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
                DateTime.UtcNow.Add(ReservationDuration),
                gift.CoupleId,
                requestContext?.CorrelationId);

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

        string normalizedStatus = PaymentStatuses.Normalize(providerResult.Status, providerResult.StatusDetail);
        payment.UpdateProviderStatus(
            normalizedStatus,
            providerResult.StatusDetail,
            providerResult.MpOrderId,
            providerResult.MpPaymentId,
            providerResult.QrCode,
            providerResult.QrCodeBase64,
            providerResult.RefundedAmount);

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
            PaymentStatuses.Normalize(providerResult.Status, providerResult.StatusDetail),
            providerResult.StatusDetail,
            providerResult.MpOrderId,
            providerResult.MpPaymentId,
            providerResult.QrCode,
            providerResult.QrCodeBase64,
            providerResult.RefundedAmount);

        await paymentRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
        Payment refreshedPayment = await paymentRepository.GetByOrderIdAsync(paymentToUpdate.OrderId, cancellationToken) ?? paymentToUpdate;
        await SynchronizeContributionStatusAsync(refreshedPayment, cancellationToken);
        return refreshedPayment;
    }

    private async Task SynchronizeContributionStatusAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (!payment.ContributionCreated || !payment.ContributionId.HasValue)
            return;

        string? targetStatus = payment.Status switch
        {
            string status when PaymentStatuses.IsSettled(status) => ContributionStatus.Paid,
            PaymentStatuses.Refunded => ContributionStatus.Refunded,
            PaymentStatuses.ChargedBack => ContributionStatus.Chargeback,
            _ => null
        };

        if (targetStatus is null)
        {
            if (payment.Status != PaymentStatuses.PartiallyRefunded)
                return;
        }

        Contribution? contribution = await contributionRepository.GetByIdAsync(payment.ContributionId.Value, cancellationToken);
        if (contribution is null)
            return;

        if (payment.Status == PaymentStatuses.PartiallyRefunded)
        {
            contribution.UpdatePaymentStatus(payment.Status);
            contribution.ApplyRefund(payment.RefundedAmount);
            await contributionRepository.SaveChangesAsync(cancellationToken);
            cacheService.Invalidate();
            return;
        }

        decimal refundedAmount = targetStatus is ContributionStatus.Refunded or ContributionStatus.Chargeback
            ? payment.Amount
            : 0;
        contribution.UpdatePaymentStatus(payment.Status);
        contribution.ApplyRefund(refundedAmount);

        if (contribution.Status == targetStatus)
        {
            await contributionRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        contribution.UpdateStatus(targetStatus, payment.UpdatedAt);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
    }

    private async Task<PaymentResponseDto?> ValidateGiftForPaymentAsync(
        Gift gift,
        decimal amount,
        string paymentMethod,
        CancellationToken cancellationToken)
    {
        bool allowsUnlimitedPurchases = await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken);

        if (allowsUnlimitedPurchases)
            return null;

        decimal remainingAmount = await GetRemainingAmountAsync(gift, cancellationToken);

        if (remainingAmount <= 0)
            return await BuildErrorResponseAsync(paymentMethod, "validation", "O presente não está disponível.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (!gift.AllowPartialContribution && amount < remainingAmount)
            return await BuildErrorResponseAsync(paymentMethod, "validation", "O presente não permite contribuição parcial.", PaymentErrorCodes.ValidationError, cancellationToken);

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
                PaymentStatuses.Normalize(providerStatus.Status, providerStatus.StatusDetail),
                providerStatus.StatusDetail,
                providerStatus.MpOrderId,
                providerStatus.MpPaymentId,
                providerStatus.QrCode,
                providerStatus.QrCodeBase64,
                providerStatus.RefundedAmount);

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
            ContributionStatus.Paid,
            payment.CoupleId,
            payment.OrderId,
            payment.PayerEmail,
            payment.CreatedAt,
            payment.Status);

        await contributionRepository.AddAsync(contribution, cancellationToken);
        payment.MarkContributionCreated(contribution.Id);
        if (operationalRepository is not null && !string.IsNullOrWhiteSpace(payment.PayerEmail))
        {
            Couple? couple = await coupleRepository.GetByIdAsync(payment.CoupleId, false, cancellationToken);
            await operationalRepository.AddEmailOutboxAsync(
                EmailOutboxMessage.Create(payment, "PaymentApproved", couple?.Names ?? string.Empty),
                cancellationToken);
        }

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
            if (backgroundTaskQueue is not null)
            {
                await backgroundTaskQueue.EnqueueAsync(async (services, queuedCancellationToken) =>
                {
                    IEmailService service = services.GetRequiredService<IEmailService>();
                    await service.SendContributionNotificationAsync(payment.ContributorName, payment.Amount, queuedCancellationToken);
                }, cancellationToken);
            }
            else
            {
                await emailService.SendContributionNotificationAsync(payment.ContributorName, payment.Amount, cancellationToken);
            }
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
            if (backgroundTaskQueue is not null)
            {
                await backgroundTaskQueue.EnqueueAsync(async (services, queuedCancellationToken) =>
                {
                    IEmailService service = services.GetRequiredService<IEmailService>();
                    await service.SendPaymentAttemptNotificationAsync(subject, body, queuedCancellationToken);
                }, cancellationToken);
            }
            else
            {
                await emailService.SendPaymentAttemptNotificationAsync(subject, body, cancellationToken);
            }
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
            if (backgroundTaskQueue is not null)
            {
                await backgroundTaskQueue.EnqueueAsync(async (services, queuedCancellationToken) =>
                {
                    IEmailService service = services.GetRequiredService<IEmailService>();
                    await service.SendErrorNotificationAsync(subject, body, queuedCancellationToken);
                }, cancellationToken);
            }
            else
            {
                await emailService.SendErrorNotificationAsync(subject, body, cancellationToken);
            }
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
            RefundedAmount = payment.RefundedAmount,
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

    private static AdminPaymentResponseDto ToAdminResponseDto(Payment payment)
        => new()
        {
            OrderId = payment.OrderId,
            GiftId = payment.GiftId,
            GiftName = payment.GiftName,
            GuestName = payment.ContributorName,
            GuestEmail = MaskEmail(payment.PayerEmail),
            Amount = payment.Amount,
            Method = payment.Method switch { "pix" => "Pix", "credit_card" => "CreditCard", "debit_card" => "DebitCard", _ => payment.Method },
            Status = string.IsNullOrWhiteSpace(payment.Status) ? string.Empty : char.ToUpperInvariant(payment.Status[0]) + payment.Status[1..],
            StatusDetail = FriendlyPaymentDetail(payment.Status),
            ProviderId = payment.MpPaymentId ?? payment.MpOrderId ?? string.Empty,
            CorrelationId = PaymentStatuses.IsSettled(payment.Status) ? string.Empty : payment.CorrelationId ?? string.Empty,
            ContributionCreated = payment.ContributionCreated,
            CreatedAtUtc = payment.CreatedAt,
            UpdatedAtUtc = payment.UpdatedAt
        };

    private static string MaskEmail(string email)
    {
        int separator = email.IndexOf('@');
        if (separator <= 0)
            return string.Empty;

        string local = email[..separator];
        string maskedLocal = local.Length <= 2 ? $"{local[0]}*" : $"{local[0]}***{local[^1]}";
        return $"{maskedLocal}{email[separator..]}";
    }

    private static string FriendlyPaymentDetail(string status) => status switch
    {
        PaymentStatuses.Approved => "Pagamento aprovado",
        PaymentStatuses.Pending or PaymentStatuses.InProcess or PaymentStatuses.ActionRequired => "Pagamento aguardando confirmacao",
        PaymentStatuses.Refunded => "Pagamento reembolsado",
        PaymentStatuses.PartiallyRefunded => "Pagamento parcialmente reembolsado",
        PaymentStatuses.Cancelled or PaymentStatuses.Canceled => "Pagamento cancelado",
        _ => "Pagamento nao aprovado"
    };

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
