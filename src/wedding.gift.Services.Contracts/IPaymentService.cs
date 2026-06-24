using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IPaymentService
{
    Task<PaymentResponseDto> ProcessCardPaymentAsync(CardPaymentRequestDto request, CancellationToken cancellationToken);
    Task<PaymentResponseDto> ProcessPixPaymentAsync(PixPaymentRequestDto request, CancellationToken cancellationToken);
    Task<PaymentResponseDto> GetPaymentStatusAsync(string nsu, CancellationToken cancellationToken);
    Task ProcessApprovedPixPaymentAsync(string mpOrderId, CancellationToken cancellationToken);
    Task ConfirmPaymentAsync(string mpOrderId, string status, CancellationToken cancellationToken);
}
