#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;
using System.Text;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
[Route("webhook")]
[Route("~/webhook")]
public sealed class WebhookController(
    IMercadoPagoService mercadoPago,
    IPaymentService payments,
    IConfiguration config,
    IWebHostEnvironment env,
    ILogger<WebhookController> logger) : ApiControllerBase
{
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ReceiveMercadoPagoNotification(
        [FromQuery(Name = "data.id")] string? dataId,
        [FromQuery] string? id,
        [FromQuery] string? type,
        [FromQuery] string? topic,
        CancellationToken cancellationToken)
    {
        string? notificationId = string.IsNullOrWhiteSpace(dataId) ? id : dataId;
        string? notificationType = string.IsNullOrWhiteSpace(type) ? topic : type;

        logger.LogInformation(
            "Webhook Mercado Pago recebido. Path={Path}, Type={Type}, DataId={DataId}, RequestId={RequestId}.",
            Request.Path,
            notificationType,
            notificationId,
            Request.Headers.TryGetValue("x-request-id", out StringValues requestIdHeader) ? requestIdHeader.ToString() : "-");

        if (!ValidateMercadoPagoSignature(Request, notificationId))
            return Unauthorized();

        if (notificationType != "payment" && notificationType != "order")
        {
            logger.LogInformation("Webhook Mercado Pago ignorado por tipo nao suportado. Type={Type}, DataId={DataId}.", notificationType, notificationId);
            return NoContent();
        }

        if (string.IsNullOrWhiteSpace(notificationId))
        {
            logger.LogWarning("Webhook Mercado Pago recebido sem identificador. Type={Type}.", notificationType);
            return NoContent();
        }

        PaymentResponseDto status = await mercadoPago.GetOrderStatusAsync(notificationId, cancellationToken);

        if (status.Status == "error")
        {
            logger.LogError(
                "Falha ao consultar status real do Mercado Pago no webhook. DataId={DataId}, ErrorCode={ErrorCode}, Message={Message}.",
                notificationId,
                status.ErrorCode,
                status.Message);
            return NoContent();
        }

        if (!string.IsNullOrWhiteSpace(status.MpOrderId))
        {
            await payments.ConfirmPaymentAsync(status.MpOrderId, status.Status, cancellationToken);

            if (status.Status == "approved") await payments.ProcessApprovedPixPaymentAsync(status.MpOrderId, cancellationToken);
        }

        return NoContent();
    }

    private bool ValidateMercadoPagoSignature(HttpRequest request, string? dataId)
    {
        string secret = config["MercadoPago:WebhookSecret"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(secret) || secret == "SECRET_GERADO_NO_PAINEL_DE_WEBHOOKS")
        {
            if (env.IsDevelopment()) return true;

            logger.LogError("MercadoPago:WebhookSecret nao configurado, rejeitando webhook.");
            return false;
        }

        if (!request.Headers.TryGetValue("x-signature", out StringValues signatureHeader))
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: header x-signature ausente.");
            return false;
        }

        if (!request.Headers.TryGetValue("x-request-id", out StringValues requestIdHeader))
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: header x-request-id ausente.");
            return false;
        }

        string? ts = null, v1 = null;

        foreach (string part in signatureHeader.ToString().Split(','))
        {
            string[] kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }

        if (ts is null || v1 is null)
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: x-signature sem ts ou v1.");
            return false;
        }

        string manifest = BuildSignatureManifest(dataId, requestIdHeader.ToString(), ts);

        using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        byte[] expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));

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

        bool isValid = CryptographicOperations.FixedTimeEquals(expected, provided);

        if (!isValid) logger.LogWarning("Webhook Mercado Pago rejeitado: assinatura invalida. DataId={DataId}.", dataId);

        return isValid;
    }

    private static string BuildSignatureManifest(string? dataId, string? requestId, string ts)
    {
        StringBuilder manifest = new();

        if (!string.IsNullOrWhiteSpace(dataId))
            manifest.Append("id:").Append(dataId.Trim()).Append(';');

        if (!string.IsNullOrWhiteSpace(requestId))
            manifest.Append("request-id:").Append(requestId.Trim()).Append(';');

        manifest.Append("ts:").Append(ts.Trim()).Append(';');

        return manifest.ToString();
    }
}
