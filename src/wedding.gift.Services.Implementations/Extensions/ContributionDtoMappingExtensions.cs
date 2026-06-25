using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class ContributionDtoMappingExtensions
{
    public static Contribution ToEntity(this ContributionCreateDto dto)
        => Contribution.Create(
            dto.GiftId,
            dto.ContributorName,
            dto.Message,
            dto.Amount,
            dto.PaymentMethod,
            dto.PaidAt,
            dto.Status);

    public static ContributionResponseDto ToResponseDto(this Contribution entity)
        => new()
        {
            Id = entity.Id,
            GiftId = entity.GiftId,
            ContributorName = entity.ContributorName,
            Message = entity.Message,
            Amount = entity.Amount,
            PaymentMethod = entity.PaymentMethod,
            PaidAt = entity.PaidAt,
            Status = entity.Status
        };
}
