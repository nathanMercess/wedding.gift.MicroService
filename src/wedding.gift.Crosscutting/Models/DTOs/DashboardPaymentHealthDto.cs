namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPaymentHealthDto
{
    public decimal ApprovalRate { get; set; }
    public decimal FailureRate { get; set; }
    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int PendingOlderThan30Minutes { get; set; }
    public int ApprovedWithoutContribution { get; set; }
    public int FailedLast24Hours { get; set; }
    public List<DashboardPaymentFailureReasonDto> TopFailureReasons { get; set; } = [];
    public DateTime? LastFailureAtUtc { get; set; }
}
