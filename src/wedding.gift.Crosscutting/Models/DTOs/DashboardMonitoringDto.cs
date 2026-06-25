namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardMonitoringDto
{
    public string DatabaseStatus { get; set; } = string.Empty;
    public string ApplicationLogsStatus { get; set; } = string.Empty;
    public string MetricsStatus { get; set; } = string.Empty;
    public int PendingPayments { get; set; }
    public int PendingPixPayments { get; set; }
    public int FailedPayments { get; set; }
    public int ApprovedPaymentsWithoutContribution { get; set; }
    public int ServerErrorRequests { get; set; }
    public int SlowRequests { get; set; }
    public decimal AverageRequestDurationMilliseconds { get; set; }
    public DateTime? LastPaymentAtUtc { get; set; }
    public DateTime? LastContributionAtUtc { get; set; }
    public DateTime? LastRequestAtUtc { get; set; }
    public List<string> Notes { get; set; } = [];
}
