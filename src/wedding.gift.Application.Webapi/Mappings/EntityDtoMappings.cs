using wedding.gift.Application.Webapi.Models.DTOs;
using wedding.gift.Application.Webapi.Models.Entities;

namespace wedding.gift.Application.Webapi.Mappings;

public static class EntityDtoMappings
{
    public static Gift ToEntity(this GiftCreateDto dto)
    {
        return new Gift
        {
            Id = Guid.NewGuid(),
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            ImageUrl = dto.ImageUrl.Trim(),
            Category = dto.Category.Trim(),
            Available = dto.Available,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static void ApplyUpdate(this Gift entity, GiftUpdateDto dto)
    {
        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description.Trim();
        entity.Price = dto.Price;
        entity.ImageUrl = dto.ImageUrl.Trim();
        entity.Category = dto.Category.Trim();
        entity.Available = dto.Available;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public static GiftResponseDto ToResponseDto(this Gift entity)
    {
        return new GiftResponseDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            Price = entity.Price,
            ImageUrl = entity.ImageUrl,
            Category = entity.Category,
            Available = entity.Available
        };
    }

    public static Contribution ToEntity(this ContributionCreateDto dto)
    {
        return new Contribution
        {
            Id = Guid.NewGuid(),
            GiftId = dto.GiftId,
            ContributorName = dto.ContributorName.Trim(),
            Message = dto.Message.Trim(),
            Amount = dto.Amount,
            PaymentMethod = dto.PaymentMethod.Trim(),
            PaidAt = dto.PaidAt == default ? DateTime.UtcNow : dto.PaidAt,
            Status = dto.Status.Trim()
        };
    }

    public static ContributionResponseDto ToResponseDto(this Contribution entity)
    {
        return new ContributionResponseDto
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
}
