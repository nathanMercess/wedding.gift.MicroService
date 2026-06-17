using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IGiftEnrichService
{
    Task<GiftEnrichResponseDto> EnrichAsync(string url, CancellationToken cancellationToken);
}
