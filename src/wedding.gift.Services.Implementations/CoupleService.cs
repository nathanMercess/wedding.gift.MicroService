using Microsoft.Extensions.Caching.Memory;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class CoupleService(ICoupleRepository coupleRepository, IMemoryCache cache) : ICoupleService
{
    private const string CacheKey = "couple:current";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    public async Task<CoupleResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        CoupleResponseDto? cached = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            Couple entity = await coupleRepository.GetAsync(false, cancellationToken);
            return entity is null ? new CoupleResponseDto() : entity.ToResponseDto();
        });

        return cached!;
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
        cache.Remove(CacheKey);

        return entity.ToResponseDto();
    }
}
