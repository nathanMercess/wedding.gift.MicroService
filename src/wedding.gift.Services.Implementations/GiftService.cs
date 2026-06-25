using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class GiftService(
    IGiftRepository giftRepository,
    IContributionRepository contributionRepository) : IGiftService
{
    public async Task<PagedResult<GiftResponseDto>> GetAllAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
    {
        if (queryParams.Page < 1)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE);

        if (queryParams.PageSize < 1 || queryParams.PageSize > 100)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE_SIZE);

        IQueryable<Gift> query = giftRepository.QueryWithContributions();

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

        int totalCount = await query.CountAsync(cancellationToken);

        List<Gift> items = await query
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
        int total = await giftRepository.CountAsync(cancellationToken);
        int completed = await giftRepository.CountFullyFundedAsync(cancellationToken);
        decimal goal = await giftRepository.SumTotalAsync(cancellationToken);
        decimal raised = await contributionRepository.SumPaidAmountAsync(cancellationToken);
        int contributors = await contributionRepository.CountUniquePaidContributorsAsync(cancellationToken);

        return new GiftStatsDto
        {
            Total = total,
            Completed = completed,
            Contributors = contributors,
            Raised = raised,
            Goal = goal
        };
    }

    public async Task<GiftResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdWithContributionsAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken)
    {
        Gift entity = dto.ToEntity();
        await giftRepository.AddAsync(entity, cancellationToken);
        await giftRepository.SaveChangesAsync(cancellationToken);
        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.ApplyUpdate(dto);
        await giftRepository.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAvailabilityAsync(Guid id, bool available, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.SetAvailability(available);
        await giftRepository.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        giftRepository.Delete(entity);
        await giftRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContributionResponseDto>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken)
    {
        bool giftExists = await giftRepository.ExistsAsync(giftId, cancellationToken);

        if (!giftExists)
            throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        IReadOnlyList<Contribution> contributions = await contributionRepository.GetByGiftIdAsync(giftId, cancellationToken);
        return contributions.Select(x => x.ToResponseDto()).ToList();
    }

    public async Task<ContributionResponseDto> ContributeAsync(Guid giftId, ContributeDto dto, CancellationToken cancellationToken)
    {
        Gift gift = await giftRepository.GetByIdAsync(giftId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        if (!gift.Available)
            throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);

        Contribution entity = Contribution.Create(
            giftId,
            dto.GuestName,
            dto.Message ?? string.Empty,
            dto.Amount,
            string.Empty,
            DateTime.UtcNow,
            ContributionStatus.Pending);

        await contributionRepository.AddAsync(entity, cancellationToken);
        await contributionRepository.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }
}
