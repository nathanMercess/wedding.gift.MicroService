using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using wedding.gift.Application.Webapi.Controllers.Base;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Controllers;

[Authorize(Roles = UserRoles.AdminOrSuperAdmin)]
[Route("admin/payments")]
public sealed class AdminPaymentsController(IPaymentService paymentService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponseDto<PagedResult<AdminPaymentResponseDto>>), StatusCodes.Status200OK)]
    public async Task<PagedResult<AdminPaymentResponseDto>> GetAll(
        [FromQuery] PaymentQueryParams query,
        CancellationToken cancellationToken)
        => await paymentService.GetAdminPaymentsAsync(query, cancellationToken);

    [HttpPost("reconcile-approved")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentReconciliationResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentReconciliationResponseDto> ReconcileApproved(CancellationToken cancellationToken)
        => await paymentService.ReconcileApprovedPaymentsAsync(cancellationToken);

    [HttpPost("{orderId:guid}/refund")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status502BadGateway)]
    public async Task<PaymentResponseDto> Refund(
        Guid orderId,
        [FromBody] PaymentRefundRequestDto request,
        CancellationToken cancellationToken)
    {
        PaymentResponseDto result = await paymentService.RefundPaymentAsync(
            orderId.ToString("D"),
            request.Amount,
            request.IdempotencyKey.GetValueOrDefault(),
            cancellationToken);
        SetRefundStatusCode(result);
        return result;
    }

    private void SetRefundStatusCode(PaymentResponseDto result)
    {
        if (result.Status != PaymentStatuses.Error)
            return;

        Response.StatusCode = result.ErrorCode switch
        {
            PaymentErrorCodes.ValidationError => StatusCodes.Status400BadRequest,
            PaymentErrorCodes.OrderNotFound => StatusCodes.Status404NotFound,
            PaymentErrorCodes.PaymentNotRefundable => StatusCodes.Status409Conflict,
            PaymentErrorCodes.InvalidRefundAmount => StatusCodes.Status409Conflict,
            PaymentErrorCodes.IdempotencyKeyAlreadyUsed => StatusCodes.Status409Conflict,
            PaymentErrorCodes.ResourceLocked => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status502BadGateway
        };
    }
}
