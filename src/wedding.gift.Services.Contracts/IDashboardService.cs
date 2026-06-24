using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IDashboardService
{
    Task<DashboardResponseDto> GetAsync(DashboardQueryDto query, CancellationToken cancellationToken);
}
