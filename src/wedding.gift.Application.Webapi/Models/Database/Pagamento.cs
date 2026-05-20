namespace wedding.gift.Application.Webapi.Models.Database;

public class Pagamento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PedidoId { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public int Parcelas { get; set; } = 1;
    public string Status { get; set; } = "pending";
    public string? NsuInfinite { get; set; }
    public string? BrCodePix { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? AtualizadoEm { get; set; }
}
