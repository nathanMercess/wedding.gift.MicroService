using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhook")]
[Route("~/webhook")]
public class WebhookController(
    IBackgroundTaskQueue queue,
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<WebhookController> logger) : ControllerBase
{
    [HttpPost("mercadopago")]
    public async Task<IActionResult> ReceiveMercadoPagoNotification(
        [FromQuery(Name = "data.id")] string? dataId,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Webhook Mercado Pago recebido. Path={Path}, Type={Type}, DataId={DataId}, RequestId={RequestId}.",
            Request.Path,
            type,
            dataId,
            Request.Headers.TryGetValue("x-request-id", out var requestIdHeader) ? requestIdHeader.ToString() : "-");

        if (!ValidateMercadoPagoSignature(Request, dataId))
            return Unauthorized();

        if (type != "payment" && type != "order")
        {
            logger.LogInformation("Webhook Mercado Pago ignorado por tipo nao suportado. Type={Type}, DataId={DataId}.", type, dataId);
            return Ok();
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            logger.LogWarning("Webhook Mercado Pago recebido sem data.id. Type={Type}.", type);
            return Ok();
        }

        // Desacopla o processamento e responde 200 IMEDIATAMENTE — evita que o MP
        // cancele/reenvie a notificação por demora (timeout preventivo).
        await queue.EnqueueAsync(async (sp, ct) =>
        {
            var mercadoPago = sp.GetRequiredService<IMercadoPagoService>();
            var payments = sp.GetRequiredService<IPaymentService>();

            // Regra de Ouro: confirma o status REAL via GET no Mercado Pago,
            // sem confiar cegamente no payload do webhook.
            var status = await mercadoPago.GetOrderStatusAsync(dataId, ct);

            if (status.Status == "error")
            {
                logger.LogError(
                    "Falha ao consultar status real do Mercado Pago no webhook. DataId={DataId}, ErrorCode={ErrorCode}, Message={Message}.",
                    dataId,
                    status.ErrorCode,
                    status.Message);
                return;
            }

            if (!string.IsNullOrWhiteSpace(status.MpOrderId))
            {
                await payments.ConfirmPaymentAsync(status.MpOrderId, status.Status, ct);

                if (status.Status == "approved")
                    await payments.ProcessApprovedPixPaymentAsync(status.MpOrderId, ct);
            }
        });

        return Ok();
    }

    private bool ValidateMercadoPagoSignature(HttpRequest request, string? dataId)
    {
        var secret = config["MercadoPago:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(secret) || secret == "SECRET_GERADO_NO_PAINEL_DE_WEBHOOKS")
        {
            // FAIL-CLOSED: sem segredo configurado, só liberamos em Development.
            if (env.IsDevelopment())
                return true;

            logger.LogError("MercadoPago:WebhookSecret não configurado — rejeitando webhook (fail-closed).");
            return false;
        }

        if (!request.Headers.TryGetValue("x-signature", out var signatureHeader))
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: header x-signature ausente.");
            return false;
        }

        if (!request.Headers.TryGetValue("x-request-id", out var requestIdHeader))
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: header x-request-id ausente.");
            return false;
        }

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.ToString().Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }

        if (ts is null || v1 is null)
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: x-signature sem ts ou v1.");
            return false;
        }

        var manifest = $"id:{dataId};request-id:{requestIdHeader};ts:{ts};";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(v1);
        }
        catch (FormatException)
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: v1 da assinatura nao esta em hexadecimal.");
            return false;
        }

        // Comparação em tempo constante (evita timing attack).
        var isValid = CryptographicOperations.FixedTimeEquals(expected, provided);

        if (!isValid)
            logger.LogWarning("Webhook Mercado Pago rejeitado: assinatura invalida. DataId={DataId}.", dataId);

        return isValid;
    }
}
