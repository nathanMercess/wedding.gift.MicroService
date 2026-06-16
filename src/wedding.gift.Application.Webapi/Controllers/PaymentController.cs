using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
public class PaymentController(IPaymentService paymentService) : ApiControllerBase
{
    [HttpPost("card")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);

        if (result.Status == "error")
        {
            if (!string.IsNullOrWhiteSpace(result.Message) &&
                (result.Message.Contains("required") || result.Message.Contains("Invalid")))
                return BadRequest(result);

            return StatusCode(StatusCodes.Status502BadGateway, result);
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("pix")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> PayWithPix([FromBody] PixPaymentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await paymentService.ProcessPixPaymentAsync(request, cancellationToken);

        if (result.Status == "error")
        {
            if (!string.IsNullOrWhiteSpace(result.Message) &&
                (result.Message.Contains("required") || result.Message.Contains("Invalid")))
                return BadRequest(result);

            return StatusCode(StatusCodes.Status502BadGateway, result);
        }

        return Ok(result);
    }

    [HttpGet("status/{mpOrderId}")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> GetPaymentStatus(string mpOrderId, CancellationToken cancellationToken)
    {
        var result = await paymentService.GetPaymentStatusAsync(mpOrderId, cancellationToken);

        if (result.Status == "error")
        {
            if (!string.IsNullOrWhiteSpace(result.Message) && result.Message.Contains("required"))
                return BadRequest(result);

            return StatusCode(StatusCodes.Status502BadGateway, result);
        }

        return Ok(result);
    }
}
