using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IApiRequestLogService
{
    Task SaveAsync(ApiRequestLogCreateDto dto, CancellationToken cancellationToken);
}
