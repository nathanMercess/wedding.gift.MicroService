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
    public async Task<PaymentResponseDto> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [AllowAnonymous]
    [HttpPost("pix")]
    public async Task<PaymentResponseDto> PayWithPix([FromBody] PixPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessPixPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [HttpGet("status/{mpOrderId}")]
    public async Task<PaymentResponseDto> GetPaymentStatus(string mpOrderId, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.GetPaymentStatusAsync(mpOrderId, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    private void SetPaymentStatusCode(PaymentResponseDto result)
    {
        if (result.Status != "error") return;

        Response.StatusCode = result.ErrorCode == PaymentErrorCodes.ValidationError
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status502BadGateway;
    }
}
