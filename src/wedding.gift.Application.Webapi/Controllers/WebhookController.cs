using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public class WebhookController(IPaymentService paymentService, IConfiguration config) : ControllerBase
{
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
}


