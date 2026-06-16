namespace wedding.gift.Application.Webapi.Models.Request;

public class PixPagamentoRequest
{
    public string PedidoId { get; set; } = string.Empty;
    public decimal Valor { get; set; }
}
