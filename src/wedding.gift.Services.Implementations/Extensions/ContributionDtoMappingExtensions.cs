using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class ContributionDtoMappingExtensions
{
    public static Contribution ToEntity(this ContributionCreateDto dto, Guid? coupleId = null)
        => Contribution.Create(
            dto.GiftId,
            dto.ContributorName,
            dto.Message,
            dto.Amount,
            dto.PaymentMethod,
            dto.PaidAt,
            dto.Status,
            coupleId);

    public static ContributionResponseDto ToResponseDto(this Contribution entity)
        => new()
        {
            Id = entity.Id,
            OrderId = entity.OrderId,
            GiftId = entity.GiftId,
            GiftName = entity.Gift?.Name ?? string.Empty,
            Category = entity.Gift?.Category,
            ContributorName = entity.ContributorName,
            GuestName = entity.ContributorName,
            GuestEmail = entity.GuestEmail,
            Message = entity.Message,
            Amount = entity.Amount,
            RefundedAmount = entity.RefundedAmount,
            NetAmount = entity.NetAmount,
            PaymentMethod = FriendlyMethod(entity.PaymentMethod),
            PaymentStatus = FriendlyStatus(entity.PaymentStatus),
            CreatedAtUtc = entity.CreatedAtUtc,
            PaidAt = entity.PaidAt,
            Status = entity.Status,
            MessageReadAtUtc = entity.MessageReadAtUtc,
            MessageArchivedAtUtc = entity.MessageArchivedAtUtc
        };

    public static ContributionResponseDto ToPublicResponseDto(this Contribution entity)
        => new()
        {
            Id = entity.Id,
            GiftId = entity.GiftId,
            GiftName = entity.Gift?.Name ?? string.Empty,
            ContributorName = entity.ContributorName,
            Message = entity.Message,
            Amount = entity.Amount,
            RefundedAmount = entity.RefundedAmount,
            NetAmount = entity.NetAmount,
            PaidAt = entity.PaidAt,
            Status = ContributionStatus.Paid
        };

    private static string FriendlyMethod(string method) => method switch
    {
        "pix" => "Pix",
        "credit_card" => "CreditCard",
        "debit_card" => "DebitCard",
        _ => method
    };

    private static string FriendlyStatus(string status)
        => string.IsNullOrWhiteSpace(status) ? string.Empty : char.ToUpperInvariant(status[0]) + status[1..];
}
