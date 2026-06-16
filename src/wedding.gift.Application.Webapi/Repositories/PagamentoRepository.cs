using Dapper;
using Microsoft.Data.SqlClient;
using wedding.gift.Application.Webapi.Models.Database;
using wedding.gift.Application.Webapi.Repositories.Interfaces;

namespace wedding.gift.Application.Webapi.Repositories;

public class PagamentoRepository(IConfiguration config) : IPagamentoRepository
{
    private readonly string connectionString = config.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");

    public async Task SalvarAsync(Pagamento pagamento, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO Pagamentos
                (Id, PedidoId, Metodo, Valor, Parcelas, Status, NsuInfinite, BrCodePix, CriadoEm)
            VALUES
                (@Id, @PedidoId, @Metodo, @Valor, @Parcelas, @Status, @NsuInfinite, @BrCodePix, @CriadoEm)";

        await using var conn = new SqlConnection(connectionString);
        var cmd = new CommandDefinition(sql, pagamento, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    public async Task AtualizarStatusAsync(string pedidoId, string status, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE Pagamentos
            SET Status = @Status, AtualizadoEm = @Agora
            WHERE PedidoId = @PedidoId";

        await using var conn = new SqlConnection(connectionString);
        var cmd = new CommandDefinition(sql, new { Status = status, Agora = DateTime.UtcNow, PedidoId = pedidoId }, cancellationToken: ct);
        await conn.ExecuteAsync(cmd);
    }

    public async Task<Pagamento?> BuscarPorNsuAsync(string nsu, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM Pagamentos WHERE NsuInfinite = @Nsu";

        await using var conn = new SqlConnection(connectionString);
        var cmd = new CommandDefinition(sql, new { Nsu = nsu }, cancellationToken: ct);
        return await conn.QueryFirstOrDefaultAsync<Pagamento>(cmd);
    }
}
