using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class EntityDtoMappings
{
    public static Gift ToEntity(this GiftCreateDto dto)
    {
        return new Gift
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            Price = dto.Price,
            Total = dto.Total > 0 ? dto.Total : dto.Price,
            Image = dto.Image.Trim(),
            Category = dto.Category.Trim(),
            Available = dto.Available,
            AllowPartialContribution = dto.AllowPartialContribution,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public static void ApplyUpdate(this Gift entity, GiftUpdateDto dto)
    {
        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description.Trim();
        entity.Price = dto.Price;
        entity.Total = dto.Total > 0 ? dto.Total : dto.Price;
        entity.Image = dto.Image.Trim();
        entity.Category = dto.Category.Trim();
        entity.Available = dto.Available;
        entity.AllowPartialContribution = dto.AllowPartialContribution;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    public static GiftResponseDto ToResponseDto(this Gift entity)
    {
        return new GiftResponseDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Total = entity.Total,
            Raised = entity.Contributions
                .Where(c => c.Status == ContributionStatus.Paid)
                .Sum(c => c.Amount),
            Image = entity.Image,
            Category = entity.Category,
            Available = entity.Available,
            AllowPartialContribution = entity.AllowPartialContribution
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

    public static CoupleResponseDto ToResponseDto(this Couple entity)
    {
        return new CoupleResponseDto
        {
            Id = entity.Id,
            Names = entity.Names,
            WeddingDate = entity.WeddingDate,
            PhotoUrl = entity.PhotoUrl,
            Message = entity.Message,
            PrimaryColor = entity.PrimaryColor,
            SecondaryColor = entity.SecondaryColor
        };
    }
}
