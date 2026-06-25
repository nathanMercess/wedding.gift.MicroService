using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class CoupleService(ICoupleRepository coupleRepository) : ICoupleService
{
    public async Task<CoupleResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        Couple entity = await coupleRepository.GetAsync(false, cancellationToken);
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
            dto.PrimaryColor,
            dto.SecondaryColor,
            dto.ToCarouselPhotosJson());

        await coupleRepository.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }
}
