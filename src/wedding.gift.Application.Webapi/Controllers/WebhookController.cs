#nullable enable

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

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
    public async Task ReceiveMercadoPagoNotification(
        [FromQuery(Name = "data.id")] string? dataId,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Webhook Mercado Pago recebido. Path={Path}, Type={Type}, DataId={DataId}, RequestId={RequestId}.",
            Request.Path,
            type,
            dataId,
            Request.Headers.TryGetValue("x-request-id", out StringValues requestIdHeader) ? requestIdHeader.ToString() : "-");

        if (!ValidateMercadoPagoSignature(Request, dataId)) throw new UnauthorizedException(ErrorCodes.UNAUTHORIZED_WEBHOOK);

        if (type != "payment" && type != "order")
        {
            logger.LogInformation("Webhook Mercado Pago ignorado por tipo nao suportado. Type={Type}, DataId={DataId}.", type, dataId);
            return;
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            logger.LogWarning("Webhook Mercado Pago recebido sem data.id. Type={Type}.", type);
            return;
        }

        PaymentResponseDto status = await mercadoPago.GetOrderStatusAsync(dataId, cancellationToken);

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
            await payments.ConfirmPaymentAsync(status.MpOrderId, status.Status, cancellationToken);

            if (status.Status == "approved") await payments.ProcessApprovedPixPaymentAsync(status.MpOrderId, cancellationToken);
        }
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

        string manifest = $"id:{dataId};request-id:{requestIdHeader};ts:{ts};";

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
}
