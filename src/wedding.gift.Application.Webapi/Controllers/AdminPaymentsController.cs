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

    [HttpPost("{orderId}/refund")]
    [Authorize(Roles = UserRoles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponseDto<PaymentResponseDto>), StatusCodes.Status200OK)]
    public async Task<PaymentResponseDto> Refund(
        string orderId,
        [FromBody] PaymentRefundRequestDto request,
        CancellationToken cancellationToken)
        => await paymentService.RefundPaymentAsync(orderId, request.Amount, cancellationToken);
}
