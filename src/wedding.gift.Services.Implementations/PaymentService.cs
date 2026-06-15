using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public class PaymentService(
    IInfinitePayService infinitePayService,
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

        var result = await infinitePayService.AuthorizeCardAsync(
            request.CardToken,
            request.Amount,
            request.Installments,
            request.Method,
            request.OrderId,
            cancellationToken);

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
            Nsu = result.Nsu
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

        var result = await infinitePayService.CreatePixTransactionAsync(
            request.Amount,
            request.OrderId,
            cancellationToken);

        if (result.Status == "error")
            return result;

        await paymentRepository.SaveAsync(new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            Method = "pix",
            Amount = request.Amount,
            Status = result.Status,
            Nsu = result.Nsu,
            PixQrCode = result.PixQrCode
        }, cancellationToken);

        return result;
    }

    public async Task<PaymentResponseDto> GetPaymentStatusAsync(
        string nsu,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nsu))
            return new PaymentResponseDto { Status = "error", Message = "NSU is required." };

        var payment = await paymentRepository.GetByNsuAsync(nsu, cancellationToken);

        if (payment?.Status == "approved")
            return new PaymentResponseDto { Status = "approved", Nsu = nsu };

        var result = await infinitePayService.GetTransactionStatusAsync(nsu, cancellationToken);

        if (result.Status == "error")
            return result;

        if (payment != null && payment.Status != result.Status)
            await paymentRepository.UpdateStatusAsync(payment.OrderId, result.Status, cancellationToken);

        return result;
    }

    public async Task UpdatePaymentStatusAsync(
        string orderId,
        string status,
        CancellationToken cancellationToken)
    {
        await paymentRepository.UpdateStatusAsync(orderId, status, cancellationToken);
    }
}
