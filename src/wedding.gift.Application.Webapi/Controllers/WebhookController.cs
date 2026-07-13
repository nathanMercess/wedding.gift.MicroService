#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
[EnableRateLimiting("webhook")]
[Route("webhook")]
[Route("~/webhook")]
public sealed class WebhookController(
    IMercadoPagoService mercadoPago,
    IPaymentService payments,
    IOptions<MercadoPagoOptions> mercadoPagoOptions,
    IWebHostEnvironment env,
    ILogger<WebhookController> logger) : ApiControllerBase
{
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ReceiveMercadoPagoNotification(
        [FromQuery(Name = "data.id")] string? dataId,
        [FromQuery] string? id,
        [FromQuery] string? type,
        [FromQuery] string? topic,
        CancellationToken cancellationToken)
    {
        (string? bodyId, string? bodyType) = await ReadNotificationMetadataAsync(Request, cancellationToken);
        string? notificationId = FirstNotEmpty(dataId, id, bodyId);
        string? notificationType = FirstNotEmpty(type, topic, bodyType);

        logger.LogInformation(
            "Webhook Mercado Pago recebido. Path={Path}, Type={Type}, DataId={DataId}, RequestId={RequestId}.",
            Request.Path,
            notificationType,
            notificationId,
            Request.Headers.TryGetValue("x-request-id", out StringValues requestIdHeader) ? requestIdHeader.ToString() : "-");

        if (!HasMercadoPagoSignatureHeaders(Request) &&
            await IsMercadoPagoPanelValidationRequestAsync(Request, cancellationToken))
        {
            logger.LogInformation("Webhook Mercado Pago: teste de URL do painel aceito.");
            return Ok();
        }

        if (!ValidateMercadoPagoSignature(Request, notificationId))
            return Unauthorized();

        if (notificationType is not ("payment" or "order" or "orders" or "chargebacks" or "topic_chargebacks_wh"))
        {
            logger.LogInformation("Webhook Mercado Pago ignorado por tipo nao suportado. Type={Type}, DataId={DataId}.", notificationType, notificationId);
            return Ok();
        }

        if (string.IsNullOrWhiteSpace(notificationId))
        {
            logger.LogWarning("Webhook Mercado Pago recebido sem identificador. Type={Type}.", notificationType);
            return Ok();
        }

        using CancellationTokenSource processingTimeout = new(TimeSpan.FromSeconds(mercadoPagoOptions.Value.WebhookProcessingTimeoutSeconds));
        using CancellationTokenSource processingCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            Request.HttpContext.RequestAborted,
            processingTimeout.Token);

        PaymentResponseDto status;
        try
        {
            bool isChargeback = notificationType is "chargebacks" or "topic_chargebacks_wh";
            PaymentResponseDto? chargeback = null;

            if (isChargeback)
            {
                chargeback = await mercadoPago.GetChargebackAsync(notificationId, processingCancellation.Token);

                if (chargeback.Status == PaymentStatuses.Error || string.IsNullOrWhiteSpace(chargeback.MpPaymentId))
                {
                    logger.LogError(
                        "Falha ao consultar chargeback no Mercado Pago. DataId={DataId}, ErrorCode={ErrorCode}, Message={Message}.",
                        notificationId,
                        chargeback.ErrorCode,
                        chargeback.Message);
                    return StatusCode(StatusCodes.Status502BadGateway);
                }
            }

            string statusProviderId = chargeback?.MpPaymentId ?? notificationId;
            status = await mercadoPago.GetOrderStatusAsync(statusProviderId, processingCancellation.Token);

            if (status.Status == "error")
            {
                logger.LogError(
                    "Falha ao consultar status real do Mercado Pago no webhook. DataId={DataId}, ErrorCode={ErrorCode}, Message={Message}.",
                    notificationId,
                    status.ErrorCode,
                    status.Message);
                return StatusCode(StatusCodes.Status502BadGateway);
            }

            if (chargeback is not null)
            {
                status.Status = chargeback.Status;
                status.StatusDetail = chargeback.StatusDetail;
                status.RefundedAmount = chargeback.RefundedAmount;
            }

            string providerId = status.MpOrderId ?? status.MpPaymentId ?? notificationId;
            await payments.ConfirmPaymentAsync(
                providerId,
                status.Status,
                status.StatusDetail,
                status.MpPaymentId,
                status.RefundedAmount,
                status.OrderId,
                status.Amount,
                status.CurrencyId,
                status.Method,
                processingCancellation.Token);
        }
        catch (OperationCanceledException) when (processingTimeout.IsCancellationRequested &&
                                                 !cancellationToken.IsCancellationRequested &&
                                                 !Request.HttpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogWarning("Webhook Mercado Pago excedeu o tempo de processamento. DataId={DataId}.", notificationId);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        return Ok();
    }

    private static bool HasMercadoPagoSignatureHeaders(HttpRequest request)
        => request.Headers.ContainsKey("x-signature") && request.Headers.ContainsKey("x-request-id");

    private static async Task<(string? Id, string? Type)> ReadNotificationMetadataAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        try
        {
            JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            using (document)
            {
                JsonElement root = document.RootElement;
                string? type = TryGetValueAsString(root, "type");
                string? id = root.TryGetProperty("data", out JsonElement data)
                    ? TryGetValueAsString(data, "id")
                    : null;
                return (id, type);
            }
        }
        catch (JsonException)
        {
            return (null, null);
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task<bool> IsMercadoPagoPanelValidationRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        request.EnableBuffering();

        try
        {
            JsonDocument document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);

            using (document)
            {
                JsonElement root = document.RootElement;

                return TryGetString(root, "action", out string action) &&
                       TryGetString(root, "api_version", out string apiVersion) &&
                       TryGetString(root, "id", out string id) &&
                       TryGetString(root, "type", out string type) &&
                       root.TryGetProperty("live_mode", out JsonElement liveMode) &&
                       liveMode.ValueKind == JsonValueKind.False &&
                       root.TryGetProperty("data", out JsonElement data) &&
                       TryGetString(data, "id", out string dataId) &&
                       action == "payment.updated" &&
                       apiVersion == "v1" &&
                       id == "123456" &&
                       dataId == "123456" &&
                       type == "payment";
            }
        }
        catch (JsonException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;

        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out JsonElement property) &&
               property.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value = property.GetString() ?? string.Empty);
    }

    private static string? TryGetValueAsString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static string? FirstNotEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private bool ValidateMercadoPagoSignature(HttpRequest request, string? dataId)
    {
        string secret = mercadoPagoOptions.Value.WebhookSecret;

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

        if (!IsRecentTimestamp(ts))
        {
            logger.LogWarning("Webhook Mercado Pago rejeitado: timestamp fora da janela permitida.");
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

    private static bool IsRecentTimestamp(string timestamp)
    {
        if (!long.TryParse(timestamp, out long value))
            return false;

        try
        {
            DateTimeOffset sentAt = value > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);

            return Math.Abs((DateTimeOffset.UtcNow - sentAt).TotalMinutes) <= 5;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string BuildSignatureManifest(string? dataId, string? requestId, string ts)
    {
        StringBuilder manifest = new();

        if (!string.IsNullOrWhiteSpace(dataId))
            manifest.Append("id:").Append(dataId.Trim().ToLowerInvariant()).Append(';');

        if (!string.IsNullOrWhiteSpace(requestId))
            manifest.Append("request-id:").Append(requestId.Trim()).Append(';');

        manifest.Append("ts:").Append(ts.Trim()).Append(';');

        return manifest.ToString();
    }
}
