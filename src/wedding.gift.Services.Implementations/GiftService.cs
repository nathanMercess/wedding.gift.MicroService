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
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE);

        if (queryParams.PageSize < 1 || queryParams.PageSize > 100)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE_SIZE);

        IQueryable<Gift> query = dbContext.Gifts.Include(x => x.Contributions).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(queryParams.Category))
            query = query.Where(x => x.Category == queryParams.Category);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(x => x.Name.Contains(queryParams.Search) || x.Description.Contains(queryParams.Search));

        if (queryParams.OnlyAvailable.HasValue)
            query = query.Where(x => x.Available == queryParams.OnlyAvailable.Value);

        query = (queryParams.OrderBy, queryParams.OrderDir) switch
        {
            (GiftSortField.Price, SortDirection.Desc) => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            (GiftSortField.Price, _) => query.OrderBy(x => x.Price).ThenBy(x => x.Name),

            (GiftSortField.Total, SortDirection.Desc) => query.OrderByDescending(x => x.Total).ThenBy(x => x.Name),
            (GiftSortField.Total, _) => query.OrderBy(x => x.Total).ThenBy(x => x.Name),

            (GiftSortField.Raised, SortDirection.Desc) => query
                .OrderByDescending(x => x.Contributions
                    .Where(c => c.Status == ContributionStatus.Paid)
                    .Sum(c => c.Amount))
                .ThenBy(x => x.Name),
            (GiftSortField.Raised, _) => query
                .OrderBy(x => x.Contributions
                    .Where(c => c.Status == ContributionStatus.Paid)
                    .Sum(c => c.Amount))
                .ThenBy(x => x.Name),

            (GiftSortField.Available, SortDirection.Desc) => query.OrderByDescending(x => x.Available).ThenBy(x => x.Name),
            (GiftSortField.Available, _) => query.OrderBy(x => x.Available).ThenBy(x => x.Name),

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
        
        var completed = await dbContext.Gifts
            .CountAsync(g => g.Contributions
                .Where(c => c.Status == ContributionStatus.Paid)
                .Sum(c => c.Amount) >= g.Total, cancellationToken);
       
        var goal = await dbContext.Gifts.SumAsync(x => x.Total, cancellationToken);
        
        var raised = await dbContext.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .SumAsync(x => x.Amount, cancellationToken);
        
        var contributors = await dbContext.Contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .Select(x => x.ContributorName.Trim().ToLower())
            .CountAsync(cancellationToken);

        return new GiftStatsDto
        {
            Total = total,
            Completed = completed,
            Contributors = contributors,
            Raised = raised,
            Goal = goal,
        };
    }

    public async Task<GiftResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await dbContext.Gifts
            .Include(x => x.Contributions)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken)
    {
        var entity = dto.ToEntity();
        dbContext.Gifts.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.ApplyUpdate(dto);

        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAvailabilityAsync(Guid id, bool available, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.Available = available;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        dbContext.Gifts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContributionResponseDto>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken)
    {
        var giftExists = await dbContext.Gifts.AnyAsync(x => x.Id == giftId, cancellationToken);

        if (!giftExists)
        {
            throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
        }

        List<Contribution> contributions = await dbContext.Contributions
            .AsNoTracking()
            .Where(x => x.GiftId == giftId)
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

        return contributions.Select(x => x.ToResponseDto()).ToList();
    }

    public async Task<ContributionResponseDto> ContributeAsync(Guid giftId, ContributeDto dto, CancellationToken cancellationToken)
    {
        var gift = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == giftId, cancellationToken)
                   ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        if (!gift.Available)
        {
            throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);
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

        return entity.ToResponseDto();
    }
}
