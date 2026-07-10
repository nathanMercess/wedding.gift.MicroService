using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IDashboardService
{
    Task<DashboardResponseDto> GetAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardOverviewResponseDto> GetOverviewAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardChartsDto> GetChartsAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardActionCenterDto> GetActionCenterAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardRevenueDto> GetRevenueAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardPaymentHealthDto> GetPaymentHealthAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardGiftInsightsDto> GetGiftInsightsAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<DashboardApiHealthDto> GetApiHealthAsync(DashboardQueryDto query, CancellationToken cancellationToken);
    Task<List<DashboardActivityFeedItemDto>> GetActivityFeedAsync(DashboardQueryDto query, CancellationToken cancellationToken);
}
