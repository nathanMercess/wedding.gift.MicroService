namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardChartsDto
{
    public List<DashboardTimeSeriesPointDto> ContributionsByDay { get; set; } = [];
    public List<DashboardStatusChartDto> PaymentsByStatus { get; set; } = [];
    public List<DashboardPaymentMethodChartDto> PaymentsByMethod { get; set; } = [];
    public List<DashboardCategoryChartDto> GiftsByCategory { get; set; } = [];
    public List<DashboardRequestStatusChartDto> RequestsByStatus { get; set; } = [];
    public List<DashboardRequestPathChartDto> RequestsByPath { get; set; } = [];
}
