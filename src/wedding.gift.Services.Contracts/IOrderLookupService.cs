using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IOrderLookupService
{
    Task RequestAsync(OrderLookupRequestDto request, CancellationToken cancellationToken);
    Task<OrderLookupResponseDto> ConsumeAsync(string token, CancellationToken cancellationToken);
    Task<string> CreateTokenAsync(Guid paymentId, CancellationToken cancellationToken);
}
