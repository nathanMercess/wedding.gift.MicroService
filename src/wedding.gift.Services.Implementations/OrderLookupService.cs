using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed class OrderLookupService(
    IPaymentRepository paymentRepository,
    IGiftRepository giftRepository,
    ICoupleRepository coupleRepository,
    IOperationalRepository operationalRepository,
    IRequestContext requestContext) : IOrderLookupService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public async Task RequestAsync(OrderLookupRequestDto request, CancellationToken cancellationToken)
    {
        string normalizedEmail = request.Email.Trim().ToUpperInvariant();
        string emailHash = Hash(normalizedEmail);
        string ipHash = Hash(requestContext.RemoteIpAddress);
        DateTime cutoff = DateTime.UtcNow.Subtract(AttemptWindow);

        int ipAttempts = await operationalRepository.LookupAttempts.CountAsync(x => x.IpHash == ipHash && x.CreatedAtUtc >= cutoff, cancellationToken);
        int emailAttempts = await operationalRepository.LookupAttempts.CountAsync(x => x.EmailHash == emailHash && x.CreatedAtUtc >= cutoff, cancellationToken);
        if (ipAttempts >= 10 || emailAttempts >= 5)
            throw new TooManyRequestsException(ErrorCodes.RATE_LIMIT_EXCEEDED);

        Payment? payment = await paymentRepository.Query()
            .FirstOrDefaultAsync(x => x.OrderId == request.OrderId.Trim() && x.PayerEmail.ToUpper() == normalizedEmail, cancellationToken);
        await operationalRepository.AddLookupAttemptAsync(OrderLookupAttempt.Create(ipHash, emailHash, payment is not null), cancellationToken);

        if (payment is not null)
        {
            Couple? couple = await coupleRepository.GetByIdAsync(payment.CoupleId, false, cancellationToken);
            await operationalRepository.AddEmailOutboxAsync(
                EmailOutboxMessage.Create(payment, $"OrderLookup:{Guid.NewGuid():N}", couple?.Names ?? string.Empty),
                cancellationToken);
        }

        await operationalRepository.AddAuditLogAsync(
            AuditLog.Create(null, payment?.CoupleId, "OrderLookupRequested", "Payment", payment?.Id.ToString() ?? string.Empty, requestContext.CorrelationId),
            cancellationToken);
        await operationalRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<OrderLookupResponseDto> ConsumeAsync(string token, CancellationToken cancellationToken)
    {
        string tokenHash = Hash(token);
        PaymentOrderLookupToken entity = await operationalRepository.LookupTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.ORDER_LOOKUP_INVALID);

        DateTime now = DateTime.UtcNow;
        if (!entity.IsValid(now))
            throw new NotFoundException(ErrorCodes.ORDER_LOOKUP_INVALID);

        Payment payment = await paymentRepository.Query().FirstOrDefaultAsync(x => x.Id == entity.PaymentId, cancellationToken)
                          ?? throw new NotFoundException(ErrorCodes.ORDER_LOOKUP_INVALID);
        Gift? gift = await giftRepository.GetByIdAsync(payment.GiftId, cancellationToken);
        entity.Consume(now);
        await operationalRepository.AddAuditLogAsync(AuditLog.Create(null, payment.CoupleId, "OrderLookupConsumed", "Payment", payment.Id.ToString(), requestContext.CorrelationId), cancellationToken);
        await operationalRepository.SaveChangesAsync(cancellationToken);

        return new OrderLookupResponseDto
        {
            OrderId = payment.OrderId,
            GiftName = payment.GiftName,
            GiftImage = gift?.Image ?? string.Empty,
            Amount = payment.Amount,
            Method = payment.Method,
            Status = FriendlyStatus(payment.Status),
            CreatedAtUtc = payment.CreatedAt,
            UpdatedAtUtc = payment.UpdatedAt,
            ContributionCreated = payment.ContributionCreated
        };
    }

    public async Task<string> CreateTokenAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        string rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        Payment? payment = await paymentRepository.Query().FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        await operationalRepository.AddLookupTokenAsync(
            PaymentOrderLookupToken.Create(paymentId, Hash(rawToken), DateTime.UtcNow.Add(TokenLifetime)),
            cancellationToken);
        await operationalRepository.AddAuditLogAsync(
            AuditLog.Create(null, payment?.CoupleId, "OrderLookupGenerated", "Payment", paymentId.ToString(), requestContext.CorrelationId),
            cancellationToken);
        await operationalRepository.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string FriendlyStatus(string status) => PaymentStatuses.IsSettled(status) ? "Approved" : status switch
    {
        PaymentStatuses.Pending or PaymentStatuses.InProcess or PaymentStatuses.ActionRequired => "Pending",
        PaymentStatuses.Refunded or PaymentStatuses.PartiallyRefunded => "Refunded",
        _ => "NotApproved"
    };
}
