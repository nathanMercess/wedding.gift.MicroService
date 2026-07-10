namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardResponseDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public DashboardPeriodDto Period { get; set; } = new();
    public DashboardOverviewDto Overview { get; set; } = new();
    public DashboardGiftSummaryDto Gifts { get; set; } = new();
    public DashboardContributionSummaryDto Contributions { get; set; } = new();
    public DashboardPaymentSummaryDto Payments { get; set; } = new();
    public DashboardMessageSummaryDto Messages { get; set; } = new();
    public DashboardRequestSummaryDto Requests { get; set; } = new();
    public DashboardMonitoringDto Monitoring { get; set; } = new();
    public DashboardActionCenterDto ActionCenter { get; set; } = new();
    public DashboardRevenueDto Revenue { get; set; } = new();
    public DashboardPaymentHealthDto PaymentHealth { get; set; } = new();
    public DashboardGiftInsightsDto GiftInsights { get; set; } = new();
    public DashboardApiHealthDto ApiHealth { get; set; } = new();
    public List<DashboardTimeSeriesPointDto> ContributionsByDay { get; set; } = [];
    public List<DashboardStatusChartDto> PaymentsByStatus { get; set; } = [];
    public List<DashboardPaymentMethodChartDto> PaymentsByMethod { get; set; } = [];
    public List<DashboardCategoryChartDto> GiftsByCategory { get; set; } = [];
    public List<DashboardRequestStatusChartDto> RequestsByStatus { get; set; } = [];
    public List<DashboardRequestPathChartDto> RequestsByPath { get; set; } = [];
    public List<DashboardGiftFundingDto> TopGiftsByRaised { get; set; } = [];
    public List<DashboardMessageDto> RecentMessages { get; set; } = [];
    public List<DashboardApiRequestActivityDto> RecentRequests { get; set; } = [];
    public List<DashboardPaymentActivityDto> RecentPayments { get; set; } = [];
    public List<DashboardPaymentActivityDto> RecentFailedPayments { get; set; } = [];
    public List<DashboardContributionActivityDto> RecentContributions { get; set; } = [];
    public List<DashboardActivityFeedItemDto> ActivityFeed { get; set; } = [];
}
