using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Models.Database;
using wedding.gift.Application.Webapi.Models.Request;
using wedding.gift.Application.Webapi.Models.Response;
using wedding.gift.Application.Webapi.Repositories.Interfaces;
using wedding.gift.Application.Webapi.Services.Interfaces;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("pagamento")]
public class PagamentoController(
    IInfinitePayService infinitePay,
    IPagamentoRepository repo) : ControllerBase
{
    [HttpPost("cartao")]
    public async Task<ActionResult<PagamentoResponse>> PagarCartao(
        [FromBody] CartaoPagamentoRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CardToken))
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "CardToken é obrigatório." });
        }

        if (string.IsNullOrWhiteSpace(req.PedidoId))
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "PedidoId é obrigatório." });
        }

        if (req.Valor <= 0)
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "Valor inválido." });
        }

        if (req.Parcelas <= 0)
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "Parcelas inválidas." });
        }

        if (req.Metodo != "credit_card" && req.Metodo != "debit_card")
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "Método inválido." });
        }

        var resultado = await infinitePay.AutorizarCartaoAsync(
            req.CardToken,
            req.Valor,
            req.Parcelas,
            req.Metodo,
            req.PedidoId,
            ct);

        if (resultado.Status == "error")
        {
            return StatusCode(StatusCodes.Status502BadGateway, resultado);
        }

        await repo.SalvarAsync(new Pagamento
        {
            PedidoId = req.PedidoId,
            Metodo = req.Metodo,
            Valor = req.Valor,
            Parcelas = req.Parcelas,
            Status = resultado.Status,
            NsuInfinite = resultado.Nsu
        }, ct);

        return Ok(resultado);
    }

    [HttpPost("pix")]
    public async Task<ActionResult<PagamentoResponse>> PagarPix(
        [FromBody] PixPagamentoRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.PedidoId))
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "PedidoId é obrigatório." });
        }

        if (req.Valor <= 0)
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "Valor inválido." });
        }

        var resultado = await infinitePay.CriarTransacaoPixAsync(req.Valor, req.PedidoId, ct);

        if (resultado.Status == "error")
        {
            return StatusCode(StatusCodes.Status502BadGateway, resultado);
        }

        await repo.SalvarAsync(new Pagamento
        {
            PedidoId = req.PedidoId,
            Metodo = "pix",
            Valor = req.Valor,
            Status = resultado.Status,
            NsuInfinite = resultado.Nsu,
            BrCodePix = resultado.BrCode
        }, ct);

        return Ok(resultado);
    }

    [HttpGet("status/{nsu}")]
    public async Task<ActionResult<PagamentoResponse>> ConsultarStatus(string nsu, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nsu))
        {
            return BadRequest(new PagamentoResponse { Status = "error", Mensagem = "NSU é obrigatório." });
        }

        var pagamento = await repo.BuscarPorNsuAsync(nsu, ct);
        if (pagamento?.Status == "approved")
        {
            return Ok(new PagamentoResponse { Status = "approved", Nsu = nsu });
        }

        var resultado = await infinitePay.ConsultarStatusAsync(nsu, ct);

        if (resultado.Status == "error")
        {
            return StatusCode(StatusCodes.Status502BadGateway, resultado);
        }

        if (pagamento != null && pagamento.Status != resultado.Status)
        {
            await repo.AtualizarStatusAsync(pagamento.PedidoId, resultado.Status, ct);
        }

        return Ok(resultado);
    }
}
