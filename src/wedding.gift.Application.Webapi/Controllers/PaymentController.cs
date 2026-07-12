using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[AllowAnonymous]
[EnableRateLimiting("payment")]
public sealed class PaymentController(IPaymentService paymentService, IOrderLookupService orderLookupService) : ApiControllerBase
{
    [HttpPost("card")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [AllowAnonymous]
    [HttpPost("pix")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> PayWithPix([FromBody] PixPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessPixPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [HttpGet("order/{orderId}")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> GetPaymentOrder(string orderId, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.GetPaymentOrderAsync(orderId, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [HttpPost("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupAcceptedDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupAcceptedDto> LookupPaymentOrder([FromBody] PaymentOrderLookupRequestDto request, CancellationToken cancellationToken)
    {
        await orderLookupService.RequestAsync(new OrderLookupRequestDto { OrderId = request.OrderId, Email = request.Email }, cancellationToken);
        return new OrderLookupAcceptedDto();
    }

    [HttpPost("order-lookup/request")]
    [EnableRateLimiting("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupAcceptedDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupAcceptedDto> RequestOrderLookup([FromBody] OrderLookupRequestDto request, CancellationToken cancellationToken)
    {
        await orderLookupService.RequestAsync(request, cancellationToken);
        return new OrderLookupAcceptedDto();
    }

    [HttpGet("order-lookup/{token}")]
    [EnableRateLimiting("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupResponseDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupResponseDto> ConsumeOrderLookup(string token, CancellationToken cancellationToken)
        => await orderLookupService.ConsumeAsync(token, cancellationToken);

    [HttpGet("status/{mpOrderId}")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> GetPaymentStatus(string mpOrderId, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.GetPaymentStatusAsync(mpOrderId, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    private void SetPaymentStatusCode(PaymentResponseDto result)
    {
        if (result.Status != "error") return;

        Response.StatusCode = result.ErrorCode switch
        {
            PaymentErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
            PaymentErrorCodes.InsufficientAmount => StatusCodes.Status409Conflict,
            PaymentErrorCodes.DuplicateOrder => StatusCodes.Status409Conflict,
            PaymentErrorCodes.OrderNotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status502BadGateway
        };
    }
}
