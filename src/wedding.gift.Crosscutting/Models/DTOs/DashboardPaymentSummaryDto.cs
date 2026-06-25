namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardPaymentSummaryDto
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
