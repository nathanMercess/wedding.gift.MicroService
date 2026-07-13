using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class GiftDtoMappingExtensions
{
    public static Gift ToEntity(this GiftCreateDto dto, Guid? coupleId = null)
        => Gift.Create(
            dto.Name,
            dto.Description,
            dto.Price > 0 ? dto.Price : dto.Total,
            dto.Total,
            dto.Image,
            dto.Category,
            dto.AllowPartialContribution,
            coupleId);

    public static void ApplyUpdate(this Gift entity, GiftUpdateDto dto)
        => entity.Update(
            dto.Name,
            dto.Description,
            dto.Price > 0 ? dto.Price : dto.Total,
            dto.Total,
            dto.Image,
            dto.Category,
            dto.AllowPartialContribution);

    public static GiftResponseDto ToResponseDto(
        this Gift entity,
        decimal reservedAmount = 0,
        bool showCategory = true,
        bool allowsUnlimitedPurchases = false)
    {
        decimal remaining = Math.Max(entity.Total - entity.RaisedAmount - reservedAmount, 0);

        return new GiftResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Total = entity.Total,
            Raised = entity.RaisedAmount,
            Remaining = remaining,
            FullyFunded = entity.Total > 0 && remaining <= 0,
            Image = entity.Image,
            Category = showCategory ? entity.Category ?? string.Empty : string.Empty,
            Available = allowsUnlimitedPurchases || remaining > 0,
            AllowPartialContribution = entity.AllowPartialContribution
        };
    }
}
