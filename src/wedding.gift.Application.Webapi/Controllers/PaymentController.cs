using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

public sealed class PaymentController(
    IPaymentService paymentService,
    IOrderLookupService orderLookupService,
    IOptions<JwtOptions> jwtOptions) : ApiControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("payment")]
    [HttpPost("card")]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> PayWithCard([FromBody] CardPaymentRequestDto request, CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.ProcessCardPaymentAsync(request, cancellationToken);
        SetPaymentStatusCode(result);
        SetOrderAccessCookie(result.OrderId);
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
        SetOrderAccessCookie(result.OrderId);
        return result;
    }

    [AllowAnonymous]
    [EnableRateLimiting("payment-polling")]
    [HttpGet("order/{orderId:guid}")]
    [ProducesResponseType(typeof(ApiResponseDto<PublicPaymentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<PublicPaymentResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<PublicPaymentResponseDto> GetPaymentOrder(Guid orderId, CancellationToken cancellationToken)
    {
        if (!HasOrderAccess(orderId))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return new PublicPaymentResponseDto
            {
                OrderId = orderId.ToString("D"),
                Status = PaymentStatuses.Error,
                ErrorCode = PaymentErrorCodes.OrderNotFound,
                Message = "O pedido de pagamento não foi encontrado."
            };
        }

        PaymentResponseDto result = await paymentService.GetPaymentOrderAsync(orderId.ToString("D"), cancellationToken);
        SetPaymentStatusCode(result);
        bool qrCodeIsActive = PaymentStatuses.IsReserving(result.Status) &&
                              result.ExpiresAt.GetValueOrDefault() > DateTime.UtcNow;

        return new PublicPaymentResponseDto
        {
            OrderId = result.OrderId ?? orderId.ToString("D"),
            GiftId = result.GiftId,
            GiftName = result.GiftName,
            Amount = result.Amount,
            Method = result.Method,
            Status = result.Status,
            StatusDetail = result.StatusDetail,
            ErrorCode = result.ErrorCode,
            Message = result.Status == PaymentStatuses.Error ? result.Message : string.Empty,
            ContributionCreated = result.ContributionCreated,
            PaidAt = result.PaidAt,
            CreatedAt = result.CreatedAt,
            UpdatedAt = result.UpdatedAt,
            ExpiresAt = result.ExpiresAt,
            RemainingAmount = result.RemainingAmount,
            QrCode = qrCodeIsActive ? result.QrCode : string.Empty,
            QrCodeBase64 = qrCodeIsActive ? result.QrCodeBase64 : null
        };
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

    private void SetOrderAccessCookie(string? orderId)
    {
        if (!Guid.TryParse(orderId, out Guid parsedOrderId))
            return;

        Response.Cookies.Append(
            GetOrderCookieName(parsedOrderId),
            CreateOrderAccessSignature(parsedOrderId),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                MaxAge = TimeSpan.FromDays(1),
                Path = $"/api/payment/order/{parsedOrderId:D}"
            });
    }

    private bool HasOrderAccess(Guid orderId)
    {
        if (!Request.Cookies.TryGetValue(GetOrderCookieName(orderId), out string? providedSignature) ||
            string.IsNullOrWhiteSpace(providedSignature))
        {
            return false;
        }

        byte[] expected = Encoding.ASCII.GetBytes(CreateOrderAccessSignature(orderId));
        byte[] provided = Encoding.ASCII.GetBytes(providedSignature.Trim());
        return expected.Length == provided.Length && CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private string CreateOrderAccessSignature(Guid orderId)
    {
        byte[] key = Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey);
        using HMACSHA256 hmac = new(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"payment-order-access:{orderId:D}")));
    }

    private static string GetOrderCookieName(Guid orderId)
        => $"wg-payment-{orderId:N}";
}
