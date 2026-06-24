namespace wedding.gift.Crosscutting.Models.DTOs;

public class DashboardResponseDto
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
}

public class DashboardPeriodDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public int Days { get; set; }
}

public class DashboardOverviewDto
{
    public decimal TotalRaised { get; set; }
    public decimal TotalGoal { get; set; }
    public decimal FundingPercent { get; set; }
    public int TotalGifts { get; set; }
    public int FullyFundedGifts { get; set; }
    public int PaidContributions { get; set; }
    public int UniqueContributors { get; set; }
    public int ApprovedPayments { get; set; }
    public int FailedPayments { get; set; }
}

public class DashboardGiftSummaryDto
{
    public int Total { get; set; }
    public int Available { get; set; }
    public int Unavailable { get; set; }
    public int FullyFunded { get; set; }
    public int PartiallyFunded { get; set; }
    public int WithoutContributions { get; set; }
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AverageFundingPercent { get; set; }
}

public class DashboardContributionSummaryDto
{
    public int Total { get; set; }
    public int Paid { get; set; }
    public int Pending { get; set; }
    public int Cancelled { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal CancelledAmount { get; set; }
    public decimal AveragePaidAmount { get; set; }
    public int UniqueContributors { get; set; }
    public int MessagesCount { get; set; }
    public int PeriodPaidCount { get; set; }
    public decimal PeriodPaidAmount { get; set; }
}

public class DashboardPaymentSummaryDto
{
    public int Total { get; set; }
    public int Approved { get; set; }
    public int Pending { get; set; }
    public int Failed { get; set; }
    public int Other { get; set; }
    public int Pix { get; set; }
    public int Card { get; set; }
    public int ApprovedWithoutContribution { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal FailedAmount { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal FailureRate { get; set; }
}

public class DashboardMessageSummaryDto
{
    public int Total { get; set; }
    public int ContributionMessages { get; set; }
    public int PaymentIntentMessages { get; set; }
    public DateTime? LatestMessageAtUtc { get; set; }
}

public class DashboardRequestSummaryDto
{
    public int Total { get; set; }
    public int Successful { get; set; }
    public int ClientErrors { get; set; }
    public int ServerErrors { get; set; }
    public int Authenticated { get; set; }
    public int Anonymous { get; set; }
    public int SlowRequests { get; set; }
    public decimal SuccessRate { get; set; }
    public decimal ClientErrorRate { get; set; }
    public decimal ServerErrorRate { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public long MaxDurationMilliseconds { get; set; }
    public DateTime? LastRequestAtUtc { get; set; }
}

public class DashboardMonitoringDto
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

public class DashboardTimeSeriesPointDto
{
    public DateTime DateUtc { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class DashboardStatusChartDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class DashboardPaymentMethodChartDto
{
    public string Method { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class DashboardCategoryChartDto
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal GoalAmount { get; set; }
    public decimal RaisedAmount { get; set; }
}

public class DashboardRequestStatusChartDto
{
    public string StatusGroup { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
}

public class DashboardRequestPathChartDto
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int Count { get; set; }
    public int ServerErrors { get; set; }
    public decimal AverageDurationMilliseconds { get; set; }
    public long MaxDurationMilliseconds { get; set; }
}

public class DashboardGiftFundingDto
{
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public decimal Raised { get; set; }
    public decimal Remaining { get; set; }
    public decimal FundingPercent { get; set; }
    public int PaidContributions { get; set; }
    public bool Available { get; set; }
    public bool FullyFunded { get; set; }
}

public class DashboardMessageDto
{
    public string Source { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string ContributorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class DashboardPaymentActivityDto
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string ContributorName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDetail { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string MpOrderId { get; set; } = string.Empty;
    public string MpPaymentId { get; set; } = string.Empty;
    public bool ContributionCreated { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class DashboardApiRequestActivityDto
{
    public Guid Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsAuthenticated { get; set; }
    public string UserRole { get; set; } = string.Empty;
    public long DurationMilliseconds { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
}

public class DashboardContributionActivityDto
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string GiftName { get; set; } = string.Empty;
    public string ContributorName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
}
