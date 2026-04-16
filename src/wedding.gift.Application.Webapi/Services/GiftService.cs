using Microsoft.EntityFrameworkCore;
using wedding.gift.Application.Webapi.Data;
using wedding.gift.Application.Webapi.Mappings;
using wedding.gift.Application.Webapi.Models.DTOs;
using wedding.gift.Application.Webapi.Models.Entities;
using wedding.gift.Application.Webapi.Services.Exceptions;

namespace wedding.gift.Application.Webapi.Services;

public class GiftService(AppDbContext dbContext) : IGiftService
{
    public async Task<IReadOnlyList<Gift>> GetAllAsync(string? category, bool? available, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Gift> query = dbContext.Gifts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x => x.Category == category);
        }

        if (available.HasValue)
        {
            query = query.Where(x => x.Available == available.Value);
        }

        query = query.OrderBy(x => x.Title);

        if (page.HasValue || pageSize.HasValue)
        {
            var safePage = page.GetValueOrDefault(1);
            var safePageSize = pageSize.GetValueOrDefault(10);

            if (safePage < 1)
            {
                throw new BadRequestException("O parâmetro 'page' deve ser maior ou igual a 1.");
            }

            if (safePageSize < 1 || safePageSize > 100)
            {
                throw new BadRequestException("O parâmetro 'pageSize' deve estar entre 1 e 100.");
            }

            query = query.Skip((safePage - 1) * safePageSize).Take(safePageSize);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Gift> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity ?? throw new NotFoundException($"Presente com id '{id}' não foi encontrado.");
    }

    public async Task<Gift> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken)
    {
        var entity = dto.ToEntity();

        dbContext.Gifts.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task<Gift> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Presente com id '{id}' não foi encontrado.");

        entity.ApplyUpdate(dto);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task<Gift> UpdateAvailabilityAsync(Guid id, bool available, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Presente com id '{id}' não foi encontrado.");

        entity.Available = available;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Presente com id '{id}' não foi encontrado.");

        dbContext.Gifts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Contribution>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken)
    {
        var giftExists = await dbContext.Gifts.AnyAsync(x => x.Id == giftId, cancellationToken);

        if (!giftExists)
        {
            throw new NotFoundException($"Presente com id '{giftId}' não foi encontrado.");
        }

        return await dbContext.Contributions
            .AsNoTracking()
            .Where(x => x.GiftId == giftId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);
    }
}
