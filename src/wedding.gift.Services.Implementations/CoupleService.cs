using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
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
            if (string.IsNullOrWhiteSpace(dto.Names) || !dto.WeddingDate.HasValue)
                throw new BadRequestException(ErrorCodes.REQUIRED_FIELDS);

            entity = Couple.Create();
            await coupleRepository.AddAsync(entity, cancellationToken);
        }

        entity.Update(
            dto.Names is null ? entity.Names : dto.Names,
            dto.WeddingDate ?? entity.WeddingDate,
            dto.PhotoUrl ?? entity.PhotoUrl,
            dto.Message ?? entity.Message,
            dto.EventLocation ?? entity.EventLocation,
            dto.PrimaryColor ?? entity.PrimaryColor,
            dto.SecondaryColor ?? entity.SecondaryColor,
            dto.GiftDisplayMode ?? entity.GiftDisplayMode,
            dto.CarouselPhotos is null ? entity.CarouselPhotosJson : dto.ToCarouselPhotosJson(),
            SiteSettingsExtensions.Merge(entity.SiteSettingsJson, dto.SiteSettings).ToSiteSettingsJson());

        try
        {
            await coupleRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ErrorCodes.CONCURRENT_MODIFICATION);
        }
        cacheService.Invalidate();

        Couple persisted = await coupleRepository.GetAsync(false, cancellationToken) ?? entity;
        return persisted.ToResponseDto();
    }
}
