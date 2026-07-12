using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IApiRequestLogService
{
    Task SaveAsync(ApiRequestLogCreateDto dto, CancellationToken cancellationToken);
    Task<int> CleanupAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
