using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Repositories.Interfaces;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhook")]
public class WebhookController(
    IPagamentoRepository repo,
    IConfiguration config) : ControllerBase
{
    [HttpPost("pagamento")]
    public async Task<ActionResult<PagamentoWebhookResponse>> ReceberWebhookCartao(
        [FromQuery] string pedido,
        [FromBody] WebhookPayload payload,
        CancellationToken ct)
    {
        if (payload.Secret != config["InfinitePay:WebhookSecret"])
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PagamentoWebhookResponse { Status = "error", Mensagem = "Webhook inválido." });
        }

        if (string.IsNullOrWhiteSpace(pedido))
        {
            return BadRequest(new PagamentoWebhookResponse { Status = "error", Mensagem = "Pedido inválido." });
        }

        await repo.AtualizarStatusAsync(pedido, payload.Status ?? "error", ct);
        return Ok(new PagamentoWebhookResponse { Status = "ok" });
    }

    [HttpPost("pix/validar")]
    public ActionResult<PagamentoWebhookResponse> ValidarPix([FromQuery] string pedido)
    {
        if (string.IsNullOrWhiteSpace(pedido))
        {
            return BadRequest(new PagamentoWebhookResponse { Status = "error", Mensagem = "Pedido inválido." });
        }

        return Ok(new PagamentoWebhookResponse { Status = "ok" });
    }

    [HttpPost("pix/confirmar")]
    public async Task<ActionResult<PagamentoWebhookResponse>> ConfirmarPix(
        [FromQuery] string pedido,
        [FromBody] WebhookPayload payload,
        CancellationToken ct)
    {
        if (payload.Secret != config["InfinitePay:WebhookSecret"])
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PagamentoWebhookResponse { Status = "error", Mensagem = "Webhook inválido." });
        }

        if (string.IsNullOrWhiteSpace(pedido))
        {
            return BadRequest(new PagamentoWebhookResponse { Status = "error", Mensagem = "Pedido inválido." });
        }

        await repo.AtualizarStatusAsync(pedido, "approved", ct);
        return Ok(new PagamentoWebhookResponse { Status = "ok" });
    }
}

public class WebhookPayload
{
    public string? Status { get; set; }
    public string? Secret { get; set; }
}

public class PagamentoWebhookResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Mensagem { get; set; }
}
