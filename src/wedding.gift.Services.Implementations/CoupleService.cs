using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class CoupleService(ICoupleRepository coupleRepository, IApplicationCacheService cacheService) : ICoupleService
{
    public async Task<CoupleResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        Couple? entity = await coupleRepository.GetAsync(false, cancellationToken);
        return entity is null ? new CoupleResponseDto() : entity.ToResponseDto();
    }

    public async Task<CoupleResponseDto> UpdateAsync(CoupleUpdateDto dto, CancellationToken cancellationToken)
    {
        Couple entity = await coupleRepository.GetAsync(true, cancellationToken);

        if (entity is null)
        {
            entity = Couple.Create();
            await coupleRepository.AddAsync(entity, cancellationToken);
        }

        entity.Update(
            dto.Names,
            dto.WeddingDate,
            dto.PhotoUrl,
            dto.Message,
            dto.EventLocation,
            dto.PrimaryColor,
            dto.SecondaryColor,
            dto.GiftDisplayMode,
            dto.ToCarouselPhotosJson());

        await coupleRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        Couple persisted = await coupleRepository.GetAsync(false, cancellationToken) ?? entity;
        return persisted.ToResponseDto();
    }
}
