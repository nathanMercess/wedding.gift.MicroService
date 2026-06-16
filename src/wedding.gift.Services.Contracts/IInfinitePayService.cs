using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IInfinitePayService
{
    Task<PaymentResponseDto> AuthorizeCardAsync(string cardToken, decimal amount, int installments, string method, string orderId, CancellationToken cancellationToken);
    Task<PaymentResponseDto> CreatePixTransactionAsync(decimal amount, string orderId, CancellationToken cancellationToken);
    Task<PaymentResponseDto> GetTransactionStatusAsync(string nsu, CancellationToken cancellationToken);
}
