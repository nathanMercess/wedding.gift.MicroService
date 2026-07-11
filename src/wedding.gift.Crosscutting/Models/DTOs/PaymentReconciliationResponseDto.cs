namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class PaymentReconciliationResponseDto
{
    public int CheckedCount { get; set; }
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<PaymentReconciliationItemDto> Items { get; set; } = [];
}
