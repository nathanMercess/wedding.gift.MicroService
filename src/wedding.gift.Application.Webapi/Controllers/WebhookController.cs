using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("webhook")]
public class WebhookController(
    IPaymentService paymentService,
    IMercadoPagoService mercadoPagoService,
    IConfiguration config) : ControllerBase
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

        var result = await mercadoPagoService.GetOrderStatusAsync(dataId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(result.MpOrderId))
            await paymentService.UpdatePaymentStatusAsync(result.MpOrderId, result.Status, cancellationToken);

        return Ok();
    }

    [HttpPost("payments")]
    public async Task<ActionResult<PaymentWebhookResponse>> ReceiveCardWebhook(
        [FromQuery] string orderId,
        [FromBody] WebhookPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.Secret != config["InfinitePay:WebhookSecret"])
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PaymentWebhookResponse { Status = "error", Message = "Invalid webhook." });
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new PaymentWebhookResponse { Status = "error", Message = "Invalid order ID." });
        }

        await paymentService.UpdatePaymentStatusAsync(orderId, payload.Status ?? "error", cancellationToken);
        return Ok(new PaymentWebhookResponse { Status = "ok" });
    }

    [HttpPost("pix/validar")]
    public ActionResult<PaymentWebhookResponse> ValidatePix([FromQuery] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new PaymentWebhookResponse { Status = "error", Message = "Invalid order ID." });
        }

        return Ok(new PaymentWebhookResponse { Status = "ok" });
    }

    [HttpPost("pix/confirmar")]
    public async Task<ActionResult<PaymentWebhookResponse>> ConfirmPix(
        [FromQuery] string orderId,
        [FromBody] WebhookPayload payload,
        CancellationToken cancellationToken)
    {
        if (payload.Secret != config["InfinitePay:WebhookSecret"])
        {
            return StatusCode(StatusCodes.Status403Forbidden, new PaymentWebhookResponse { Status = "error", Message = "Invalid webhook." });
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new PaymentWebhookResponse { Status = "error", Message = "Invalid order ID." });
        }

        await paymentService.UpdatePaymentStatusAsync(orderId, "approved", cancellationToken);
        return Ok(new PaymentWebhookResponse { Status = "ok" });
    }

    private bool ValidateMercadoPagoSignature(HttpRequest request, string? dataId)
    {
        var secret = config["MercadoPago:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
            return true;

        if (!request.Headers.TryGetValue("x-signature", out var signatureHeader))
            return false;

        if (!request.Headers.TryGetValue("x-request-id", out var requestIdHeader))
            return false;

        var parts = signatureHeader.ToString().Split(',');
        string? ts = null, v1 = null;
        foreach (var part in parts)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "ts") ts = kv[1].Trim();
            if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
        }

        if (ts == null || v1 == null) return false;

        var manifest = $"id:{dataId};request-id:{requestIdHeader};ts:{ts};";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(manifest));
        var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

        return hashHex == v1;
    }
}



