namespace wedding.gift.Application.Webapi.Models.Response;

public class PagamentoResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Nsu { get; set; }
    public string? BrCode { get; set; }
    public string? Mensagem { get; set; }
}
