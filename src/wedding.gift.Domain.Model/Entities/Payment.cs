namespace wedding.gift.Domain.Model.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid GiftId { get; set; }
    public string ContributorName { get; set; } = string.Empty;
    public Guid? ContributionId { get; set; }
    public Contribution? Contribution { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? StatusDetail { get; set; }
    public string Nsu { get; set; } = string.Empty;
    public string? MpOrderId { get; set; }
    public string? MpPaymentId { get; set; }
    public string PixQrCode { get; set; } = string.Empty;
    public string? QrCodeBase64 { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
