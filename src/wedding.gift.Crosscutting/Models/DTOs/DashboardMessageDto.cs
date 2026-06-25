namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardMessageDto
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
