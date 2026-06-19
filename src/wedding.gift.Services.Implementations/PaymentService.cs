using Microsoft.Extensions.Logging;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class PaymentService(
    IMercadoPagoService mercadoPagoService,
    IPaymentRepository paymentRepository,
    IContributionService contributionService,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponseDto> ProcessCardPaymentAsync(
        CardPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.GiftId == Guid.Empty)
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "GiftId is required." };

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "ContributorName is required." };

        if (string.IsNullOrWhiteSpace(request.CardToken))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "CardToken is required." };

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "OrderId is required." };

        if (request.Amount <= 0)
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "Invalid amount." };

        if (request.Installments <= 0)
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "Invalid installments." };

        if (request.Method != "credit_card" && request.Method != "debit_card")
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "Invalid method." };

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "PaymentMethodId is required." };

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "PayerEmail is required." };

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "PayerDocNumber is required." };

        var result = await mercadoPagoService.CreateCardOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return result;

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
        if (request.GiftId == Guid.Empty)
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "GiftId is required." };

        if (string.IsNullOrWhiteSpace(request.ContributorName))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "ContributorName is required." };

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "OrderId is required." };

        if (request.Amount <= 0)
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "Invalid amount." };

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "PayerEmail is required." };

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "PayerDocNumber (CPF) is required for Pix." };

        var result = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return result;

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
            return new PaymentResponseDto { Status = "error", ErrorCode = PaymentErrorCodes.ValidationError, Message = "MpOrderId is required." };

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
            return result;

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
            logger.LogWarning("Webhook: pagamento com MpOrderId={MpOrderId} não encontrado para confirmar.", mpOrderId);
            return;
        }

        // Atualiza o Payment pelo OrderId REAL (o webhook traz o MpOrderId, não o OrderId).
        await paymentRepository.UpdateStatusAsync(payment.OrderId, status, payment.StatusDetail, cancellationToken);

        // Quando aprovado, promove a contribuição (Pending -> Paid), o que recalcula a
        // disponibilidade do presente. Sem isso, um PIX aprovado nunca "concluía" o presente.
        if (status == "approved" && payment.ContributionId.HasValue)
        {
            await contributionService.UpdateStatusAsync(
                payment.ContributionId.Value, ContributionStatus.Paid, DateTime.UtcNow, cancellationToken);
        }
    }
}
