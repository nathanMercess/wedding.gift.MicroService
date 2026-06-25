using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class CoupleService(AppDbContext dbContext) : ICoupleService
{
    public async Task<CoupleResponseDto> GetAsync(CancellationToken cancellationToken)
    {
        Couple entity = await dbContext.Couples.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return entity is null ? new CoupleResponseDto() : entity.ToResponseDto();
    }

    public async Task<CoupleResponseDto> UpdateAsync(CoupleUpdateDto dto, CancellationToken cancellationToken)
    {
        Couple entity = await dbContext.Couples.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new Couple { Id = Guid.NewGuid() };
            dbContext.Couples.Add(entity);
        }

        entity.Names = dto.Names.Trim();
        entity.WeddingDate = dto.WeddingDate;
        entity.PhotoUrl = dto.PhotoUrl.Trim();
        entity.Message = dto.Message.Trim();
        entity.PrimaryColor = string.IsNullOrWhiteSpace(dto.PrimaryColor) ? "#C79A6D" : dto.PrimaryColor.Trim();
        entity.SecondaryColor = string.IsNullOrWhiteSpace(dto.SecondaryColor) ? "#F7F0EA" : dto.SecondaryColor.Trim();

        List<CarouselPhotoDto> photos = dto.CarouselPhotos?
            .Where(p => !string.IsNullOrWhiteSpace(p.Url))
            .Select(p => new CarouselPhotoDto { Url = p.Url.Trim(), Tag = p.Tag.Trim(), Title = p.Title.Trim() })
            .ToList();
        entity.CarouselPhotosJson = photos is { Count: > 0 } ? JsonSerializer.Serialize(photos) : null;

        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }
}
