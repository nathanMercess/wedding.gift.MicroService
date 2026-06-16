using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class PaymentService(
    IMercadoPagoService mercadoPagoService,
    IPaymentRepository paymentRepository) : IPaymentService
{
    public async Task<PaymentResponseDto> ProcessCardPaymentAsync(
        CardPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CardToken))
            return new PaymentResponseDto { Status = "error", Message = "CardToken is required." };

        if (string.IsNullOrWhiteSpace(request.OrderId))
            return new PaymentResponseDto { Status = "error", Message = "OrderId is required." };

        if (request.Amount <= 0)
            return new PaymentResponseDto { Status = "error", Message = "Invalid amount." };

        if (request.Installments <= 0)
            return new PaymentResponseDto { Status = "error", Message = "Invalid installments." };

        if (request.Method != "credit_card" && request.Method != "debit_card")
            return new PaymentResponseDto { Status = "error", Message = "Invalid method." };

        if (string.IsNullOrWhiteSpace(request.PaymentMethodId))
            return new PaymentResponseDto { Status = "error", Message = "PaymentMethodId is required." };

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return new PaymentResponseDto { Status = "error", Message = "PayerEmail is required." };

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return new PaymentResponseDto { Status = "error", Message = "PayerDocNumber is required." };

        var result = await mercadoPagoService.CreateCardOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return result;

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Method = request.Method,
            Amount = request.Amount,
            Installments = request.Installments,
            Status = result.Status,
            StatusDetail = result.StatusDetail,
            MpOrderId = result.MpOrderId,
            MpPaymentId = result.MpPaymentId
        }, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> ProcessPixPaymentAsync(
        PixPaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
            return new PaymentResponseDto { Status = "error", Message = "OrderId is required." };

        if (request.Amount <= 0)
            return new PaymentResponseDto { Status = "error", Message = "Invalid amount." };

        if (string.IsNullOrWhiteSpace(request.PayerEmail))
            return new PaymentResponseDto { Status = "error", Message = "PayerEmail is required." };

        if (string.IsNullOrWhiteSpace(request.PayerDocNumber))
            return new PaymentResponseDto { Status = "error", Message = "PayerDocNumber (CPF) is required for Pix." };

        var result = await mercadoPagoService.CreatePixOrderAsync(request, cancellationToken);

        if (result.Status == "error")
            return result;

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Method = "pix",
            Amount = request.Amount,
            Status = result.Status,
            StatusDetail = result.StatusDetail,
            MpOrderId = result.MpOrderId,
            MpPaymentId = result.MpPaymentId,
            PixQrCode = result.QrCode,
            QrCodeBase64 = result.QrCodeBase64
        }, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string mpOrderId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mpOrderId))
            return new PaymentResponseDto { Status = "error", Message = "MpOrderId is required." };

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

    public async Task UpdatePaymentStatusAsync(
        string orderId,
        string status,
        CancellationToken cancellationToken)
    {
        await paymentRepository.UpdateStatusAsync(orderId, status, null, cancellationToken);
    }
}
