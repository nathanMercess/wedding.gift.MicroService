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
            string.IsNullOrWhiteSpace(dto.Names) ? entity.Names : dto.Names,
            dto.WeddingDate == default ? entity.WeddingDate : dto.WeddingDate,
            string.IsNullOrWhiteSpace(dto.PhotoUrl) ? entity.PhotoUrl : dto.PhotoUrl,
            string.IsNullOrWhiteSpace(dto.Message) ? entity.Message : dto.Message,
            string.IsNullOrWhiteSpace(dto.EventLocation) ? entity.EventLocation : dto.EventLocation,
            string.IsNullOrWhiteSpace(dto.PrimaryColor) ? entity.PrimaryColor : dto.PrimaryColor,
            string.IsNullOrWhiteSpace(dto.SecondaryColor) ? entity.SecondaryColor : dto.SecondaryColor,
            string.IsNullOrWhiteSpace(dto.GiftDisplayMode) ? entity.GiftDisplayMode : dto.GiftDisplayMode,
            dto.ToCarouselPhotosJson(),
            SiteSettingsExtensions.Merge(entity.SiteSettingsJson, dto.SiteSettings).ToSiteSettingsJson());

        await coupleRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        Couple persisted = await coupleRepository.GetAsync(false, cancellationToken) ?? entity;
        return persisted.ToResponseDto();
    }
}
