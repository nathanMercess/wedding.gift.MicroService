using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
public sealed class PaymentController(IPaymentService paymentService) : ApiControllerBase
{
    [HttpPost("card")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);
        return ToPaymentActionResult(result);
    }

    [AllowAnonymous]
    [HttpPost("pix")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> PayWithPix([FromBody] PixPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessPixPaymentAsync(request, cancellationToken);
        return ToPaymentActionResult(result);
    }

    [HttpGet("status/{mpOrderId}")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PaymentResponseDto>> GetPaymentStatus(string mpOrderId, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.GetPaymentStatusAsync(mpOrderId, cancellationToken);
        return ToPaymentActionResult(result);
    }

    private ActionResult<PaymentResponseDto> ToPaymentActionResult(PaymentResponseDto result)
    {
        if (result.Status != "error") return Ok(result);

        if (result.ErrorCode == PaymentErrorCodes.ValidationError) return BadRequest(result);

        return StatusCode(StatusCodes.Status502BadGateway, result);
    }
}
