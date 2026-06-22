using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public class GiftService(AppDbContext dbContext) : IGiftService
{
    public async Task<PagedResult<GiftResponseDto>> GetAllAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
    {
        if (queryParams.Page < 1)
            throw new BadRequestException("O parâmetro 'page' deve ser maior ou igual a 1.");

        if (queryParams.PageSize < 1 || queryParams.PageSize > 100)
            throw new BadRequestException("O parâmetro 'pageSize' deve estar entre 1 e 100.");

        IQueryable<Gift> query = dbContext.Gifts.Include(x => x.Contributions).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(queryParams.Category))
            query = query.Where(x => x.Category == queryParams.Category);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(x => x.Name.Contains(queryParams.Search) || x.Description.Contains(queryParams.Search));

        if (queryParams.OnlyAvailable.HasValue)
            query = query.Where(x => x.Available == queryParams.OnlyAvailable.Value);

        query = (queryParams.OrderBy, queryParams.OrderDir) switch
        {
            (GiftSortField.Price, SortDirection.Desc) => query.OrderByDescending(x => x.Price),
            (GiftSortField.Price, _) => query.OrderBy(x => x.Price),

            (GiftSortField.Available, SortDirection.Desc) => query.OrderByDescending(x => x.Available),
            (GiftSortField.Available, _) => query.OrderBy(x => x.Available),

            (GiftSortField.Name, SortDirection.Desc) => query.OrderByDescending(x => x.Name),
            _ => query.OrderBy(x => x.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<GiftResponseDto>
        {
            Items = items.Select(g => g.ToResponseDto()),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<GiftStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var total = await dbContext.Gifts.CountAsync(cancellationToken);
        var completed = await dbContext.Gifts.CountAsync(x => !x.Available, cancellationToken);
        var goal = await dbContext.Gifts.SumAsync(x => x.Total, cancellationToken);
        var raised = await dbContext.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .SumAsync(x => x.Amount, cancellationToken);

        return new GiftStatsDto
        {
            Total = total,
            Completed = completed,
            Raised = raised,
            Goal = goal,
        };
    }

    public async Task<Gift> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts
            .Include(x => x.Contributions)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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

        var paidTotal = await dbContext.Contributions
            .Where(x => x.GiftId == id && x.Status == ContributionStatus.Paid)
            .SumAsync(x => x.Amount, cancellationToken);

        entity.Available = paidTotal < entity.Total;

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

    public async Task<Contribution> ContributeAsync(Guid giftId, ContributeDto dto, CancellationToken cancellationToken)
    {
        var gift = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == giftId, cancellationToken)
                   ?? throw new NotFoundException($"Presente com id '{giftId}' não foi encontrado.");

        if (!gift.Available)
        {
            throw new ConflictException("Não é permitido contribuir para um presente indisponível.");
        }

        var entity = new Contribution
        {
            Id = Guid.NewGuid(),
            GiftId = giftId,
            ContributorName = dto.GuestName.Trim(),
            Message = dto.Message?.Trim() ?? string.Empty,
            Amount = dto.Amount,
            PaymentMethod = string.Empty,
            PaidAt = DateTime.UtcNow,
            Status = ContributionStatus.Pending
        };

        dbContext.Contributions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
