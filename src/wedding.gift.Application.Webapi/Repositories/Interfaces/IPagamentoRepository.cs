using wedding.gift.Application.Webapi.Models.Database;

namespace wedding.gift.Application.Webapi.Repositories.Interfaces;

public interface IPagamentoRepository
{
    Task SalvarAsync(Pagamento pagamento, CancellationToken ct = default);
    Task AtualizarStatusAsync(string pedidoId, string status, CancellationToken ct = default);
    Task<Pagamento?> BuscarPorNsuAsync(string nsu, CancellationToken ct = default);
}
