using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class GiftDtoMappingExtensions
{
    public static Gift ToEntity(this GiftCreateDto dto)
        => Gift.Create(
            dto.Name,
            dto.Description,
            dto.Price,
            dto.Total,
            dto.Image,
            dto.Category ?? string.Empty,
            dto.Available,
            dto.AllowPartialContribution);

    public static void ApplyUpdate(this Gift entity, GiftUpdateDto dto)
        => entity.Update(
            dto.Name,
            dto.Description,
            dto.Price,
            dto.Total,
            dto.Image,
            dto.Category ?? string.Empty,
            dto.Available,
            dto.AllowPartialContribution);

    public static GiftResponseDto ToResponseDto(this Gift entity, bool hideCategory = false)
        => new()
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Total = entity.Total,
            Raised = entity.RaisedAmount,
            FullyFunded = entity.FullyFunded,
            Image = entity.Image,
            Category = hideCategory ? string.Empty : entity.Category,
            Available = entity.Available,
            AllowPartialContribution = entity.AllowPartialContribution
        };
}
