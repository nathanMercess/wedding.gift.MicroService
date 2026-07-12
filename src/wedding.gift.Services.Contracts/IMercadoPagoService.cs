using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IMercadoPagoService
{
    Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken);
    Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken);
    Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken);
    Task<PaymentResponseDto> RefundAsync(
        string? mpOrderId,
        string? mpPaymentId,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => Task.FromResult(new PaymentResponseDto
        {
            Status = "error",
            ErrorCode = "PROVIDER_ERROR",
            Message = "Reembolso não implementado pelo provedor configurado."
        });
    Task<PaymentResponseDto> RefundAsync(
        string? mpOrderId,
        string? mpPaymentId,
        decimal? amount,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => RefundAsync(mpOrderId, mpPaymentId, idempotencyKey, cancellationToken);
}
