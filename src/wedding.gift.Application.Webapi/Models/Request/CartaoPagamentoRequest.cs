namespace wedding.gift.Application.Webapi.Models.Request;

public class CartaoPagamentoRequest
{
    public string PedidoId { get; set; } = string.Empty;
    public string CardToken { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public int Parcelas { get; set; } = 1;
    public string Metodo { get; set; } = "credit_card";
}
