using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class PaymentController(IPaymentService paymentService, IOrderLookupService orderLookupService) : ApiControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("payment")]
    [HttpPost("card")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [AllowAnonymous]
    [EnableRateLimiting("payment")]
    [HttpPost("pix")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> PayWithPix([FromBody] PixPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessPixPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [AllowAnonymous]
    [EnableRateLimiting("payment-polling")]
    [HttpGet("order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> GetPaymentOrder(Guid orderId, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.GetPaymentOrderAsync(orderId.ToString("D"), cancellationToken);
        SetPaymentStatusCode(result);
        return result;
    }

    [AllowAnonymous]
    [EnableRateLimiting("order-lookup")]
    [HttpPost("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupAcceptedDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupAcceptedDto> LookupPaymentOrder([FromBody] PaymentOrderLookupRequestDto request, CancellationToken cancellationToken)
    {
        await orderLookupService.RequestAsync(new OrderLookupRequestDto { OrderId = request.OrderId, Email = request.Email }, cancellationToken);
        return new OrderLookupAcceptedDto();
    }

    [AllowAnonymous]
    [HttpPost("order-lookup/request")]
    [EnableRateLimiting("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupAcceptedDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupAcceptedDto> RequestOrderLookup([FromBody] OrderLookupRequestDto request, CancellationToken cancellationToken)
    {
        await orderLookupService.RequestAsync(request, cancellationToken);
        return new OrderLookupAcceptedDto();
    }

    [AllowAnonymous]
    [HttpGet("order-lookup/{token}")]
    [EnableRateLimiting("order-lookup")]
    [ProducesResponseType(typeof(ApiResponseDto<OrderLookupResponseDto>), StatusCodes.Status200OK)]
    public async Task<OrderLookupResponseDto> ConsumeOrderLookup(string token, CancellationToken cancellationToken)
        => await orderLookupService.ConsumeAsync(token, cancellationToken);

    [Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
    [EnableRateLimiting("payment-polling")]
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
