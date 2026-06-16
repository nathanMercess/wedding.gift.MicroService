using wedding.gift.Application.Webapi.Models.Response;

namespace wedding.gift.Application.Webapi.Services.Interfaces;

public interface IInfinitePayService
{
    Task<PagamentoResponse> AutorizarCartaoAsync(
        string cardToken,
        decimal valor,
        int parcelas,
        string metodo,
        string pedidoId,
        CancellationToken ct = default);

    Task<PagamentoResponse> CriarTransacaoPixAsync(
        decimal valor,
        string pedidoId,
        CancellationToken ct = default);

    Task<PagamentoResponse> ConsultarStatusAsync(
        string nsu,
        CancellationToken ct = default);
}
