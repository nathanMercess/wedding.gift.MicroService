using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations;

public sealed class PaymentService(
    IMercadoPagoService mercadoPagoService,
    AppDbContext dbContext,
    IPaymentRepository paymentRepository,
    IContributionService contributionService,
    IEmailService emailService,
    IBackgroundTaskQueue backgroundTaskQueue,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const int CreditCardMaxInstallments = 12;
    private const decimal CreditCardFeePercentForBrickAmount = 22.26m;
    private static readonly IReadOnlyDictionary<int, decimal> CreditCardFeePercentByInstallment = new Dictionary<int, decimal>
    {
        [1] = 7.97m,
        [2] = 7.51m,
        [3] = 9.60m,
        [4] = 11.67m,
        [5] = 13.64m,
        [6] = 14.94m,
        [7] = 16.22m,
        [8] = 17.48m,
        [9] = 18.71m,
        [10] = 18.91m,
        [11] = 21.10m,
        [12] = CreditCardFeePercentForBrickAmount
    };

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

        if (request.NetAmount <= 0)
            return await BuildErrorResponseAsync("card", "validation", "Invalid net amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Amount < request.NetAmount)
            return await BuildErrorResponseAsync("card", "validation", "Amount cannot be less than net amount.", PaymentErrorCodes.ValidationError, cancellationToken);

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

        Gift gift = await dbContext.Gifts
            .Include(x => x.Contributions)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.GiftId, cancellationToken);

        if (gift is null)
            return await BuildErrorResponseAsync("card", "validation", "Gift not found.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (!gift.Available)
            return await BuildErrorResponseAsync("card", "validation", "Gift is not available.", PaymentErrorCodes.ValidationError, cancellationToken);

        decimal raised = gift.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .Sum(x => x.Amount);
        decimal remainingAmount = gift.Total - raised;

        if (request.NetAmount > remainingAmount)
            return await BuildErrorResponseAsync("card", "validation", "Net amount exceeds remaining gift amount.", PaymentErrorCodes.ValidationError, cancellationToken);

        if (request.Method == "credit_card")
        {
            if (!CreditCardFeePercentByInstallment.ContainsKey(request.Installments))
                return await BuildErrorResponseAsync("card", "validation", "Installments exceed the card limit.", PaymentErrorCodes.ValidationError, cancellationToken);

            decimal expectedAmount = CalculateExpectedCardAmount(request.NetAmount, CreditCardFeePercentForBrickAmount);

            if (Math.Abs(expectedAmount - request.Amount) > 0.01m)
                return await BuildErrorResponseAsync("card", "validation", "Amount does not match the configured credit card fee.", PaymentErrorCodes.ValidationError, cancellationToken);
        }
        else if (Math.Abs(request.NetAmount - request.Amount) > 0.01m)
        {
            return await BuildErrorResponseAsync("card", "validation", "Debit card amount must match net amount.", PaymentErrorCodes.ValidationError, cancellationToken);
        }

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
                Amount = request.NetAmount,
                PaymentMethod = request.Method,
                Status = ContributionStatus.Paid,
                PaidAt = DateTime.UtcNow
            }, cancellationToken);

            contributionId = contribution.Id;

            string contributorName = request.ContributorName;
            decimal amount = request.NetAmount;
            await backgroundTaskQueue.EnqueueAsync(async (sp, ct) =>
            {
                IEmailService email = sp.GetRequiredService<IEmailService>();
                await email.SendContributionNotificationAsync(contributorName, amount, ct);
            }, cancellationToken);
        }

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = request.GiftId,
            ContributorName = request.ContributorName,
            Message = request.Message?.Trim() ?? string.Empty,
            PayerEmail = request.PayerEmail,
            PayerDocType = request.PayerDocType,
            PayerDocNumber = request.PayerDocNumber,
            ContributionId = contributionId,
            ContributionCreated = contributionId.HasValue,
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

        PaymentResponseDto result = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return await BuildErrorResponseAsync("pix", "mercado_pago", result, cancellationToken);

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            GiftId = request.GiftId,
            ContributorName = request.ContributorName,
            Message = request.Message?.Trim() ?? string.Empty,
            PayerEmail = request.PayerEmail,
            PayerDocType = request.PayerDocType,
            PayerDocNumber = request.PayerDocNumber,
            ContributionCreated = false,
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

        Payment existingPayment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MpOrderId == mpOrderId, cancellationToken);

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

            Payment paymentToUpdate = await dbContext.Payments
                .FirstOrDefaultAsync(x => x.MpOrderId == mpOrderId, cancellationToken);

            if (paymentToUpdate is null)
            {
                logger.LogError("Pix aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada ao atualizar status.", mpOrderId);
                return;
            }

            paymentToUpdate.Status = providerStatus.Status;
            paymentToUpdate.StatusDetail = providerStatus.StatusDetail;
            paymentToUpdate.MpPaymentId = providerStatus.MpPaymentId ?? paymentToUpdate.MpPaymentId;
            await dbContext.SaveChangesAsync(cancellationToken);

            existingPayment = await dbContext.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MpOrderId == mpOrderId, cancellationToken);
        }

        if (existingPayment?.ContributionCreated == true)
            return;

        if (!string.Equals(existingPayment?.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return;

        await using IDbContextTransaction transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        Payment payment = await dbContext.Payments
            .FirstOrDefaultAsync(x => x.MpOrderId == mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogError("Pix aprovado com MpOrderId={MpOrderId}, mas a intencao de pagamento nao foi encontrada dentro da transacao.", mpOrderId);
            return;
        }

        if (payment.ContributionCreated)
            return;

        if (!string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
            return;

        bool giftExists = await dbContext.Gifts.AnyAsync(x => x.Id == payment.GiftId, cancellationToken);

        if (!giftExists)
        {
            logger.LogError(
                "Pix aprovado com MpOrderId={MpOrderId}, OrderId={OrderId}, GiftId={GiftId}, mas o presente nao foi encontrado.",
                payment.MpOrderId,
                payment.OrderId,
                payment.GiftId);
            return;
        }

        Contribution contribution = new()
        {
            Id = Guid.NewGuid(),
            GiftId = payment.GiftId,
            ContributorName = payment.ContributorName.Trim(),
            Message = payment.Message.Trim(),
            Amount = payment.Amount,
            PaymentMethod = "pix",
            PaidAt = DateTime.UtcNow,
            Status = ContributionStatus.Paid
        };

        dbContext.Contributions.Add(contribution);

        payment.ContributionId = contribution.Id;
        payment.ContributionCreated = true;
        payment.Status = "approved";

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

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

        Payment payment = await dbContext.Payments
            .FirstOrDefaultAsync(x => x.MpOrderId == mpOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Webhook: pagamento com MpOrderId={MpOrderId} nao encontrado para confirmar.", mpOrderId);
            return;
        }

        if (!string.Equals(payment.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = status;
            await dbContext.SaveChangesAsync(cancellationToken);
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

    private static decimal CalculateExpectedCardAmount(decimal netAmount, decimal feePercent)
    {
        if (feePercent <= 0)
            return Math.Round(netAmount, 2, MidpointRounding.AwayFromZero);

        decimal feeFactor = 1 - (feePercent / 100);
        return Math.Round(netAmount / feeFactor, 2, MidpointRounding.AwayFromZero);
    }
}
