namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardOverviewResponseDto
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
}
