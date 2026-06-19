using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhook")]
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
        if (!ValidateMercadoPagoSignature(Request, dataId))
            return Unauthorized();

        if (type != "payment" && type != "order")
            return Ok();

        if (string.IsNullOrWhiteSpace(dataId))
            return Ok();

        // Desacopla o processamento e responde 200 IMEDIATAMENTE — evita que o MP
        // cancele/reenvie a notificação por demora (timeout preventivo).
        await queue.EnqueueAsync(async (sp, ct) =>
        {
            var mercadoPago = sp.GetRequiredService<IMercadoPagoService>();
            var payments = sp.GetRequiredService<IPaymentService>();

            // Regra de Ouro: confirma o status REAL via GET no Mercado Pago,
            // sem confiar cegamente no payload do webhook.
            var status = await mercadoPago.GetOrderStatusAsync(dataId, ct);

            if (!string.IsNullOrWhiteSpace(status.MpOrderId))
                await payments.ConfirmPaymentAsync(status.MpOrderId, status.Status, ct);
        });

        return Ok();
    }

    private bool ValidateMercadoPagoSignature(HttpRequest request, string? dataId)
    {
        var secret = config["MercadoPago:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            // FAIL-CLOSED: sem segredo configurado, só liberamos em Development.
            if (env.IsDevelopment())
                return true;

            logger.LogError("MercadoPago:WebhookSecret não configurado — rejeitando webhook (fail-closed).");
            return false;
        }

        if (!request.Headers.TryGetValue("x-signature", out var signatureHeader))
            return false;

        if (!request.Headers.TryGetValue("x-request-id", out var requestIdHeader))
            return false;

        string? ts = null, v1 = null;
        foreach (var part in signatureHeader.ToString().Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }

        if (ts is null || v1 is null)
            return false;

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
            return false;
        }

        // Comparação em tempo constante (evita timing attack).
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }
}
