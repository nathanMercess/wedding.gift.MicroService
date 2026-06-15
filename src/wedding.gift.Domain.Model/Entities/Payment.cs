namespace wedding.gift.Domain.Model.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Installments { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Nsu { get; set; } = string.Empty;
    public string PixQrCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
