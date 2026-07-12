using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    IContributionRepository contributionRepository,
    IPaymentRepository paymentRepository,
    ICoupleRepository coupleRepository,
    IMemoryCache cache,
    IApplicationCacheService cacheService) : IGiftService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<PagedResult<GiftResponseDto>> GetPublicAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        SiteSettingsDto settings = SiteSettingsExtensions.Normalize(couple?.SiteSettingsJson);

        return await GetAllCoreAsync(queryParams, settings, cancellationToken);
    }

    public async Task<PagedResult<GiftResponseDto>> GetAllAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
        => await GetPublicAsync(queryParams, cancellationToken);

    public async Task<PagedResult<GiftResponseDto>> GetAllAdminAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
        => await GetAllCoreAsync(queryParams, null, cancellationToken);

    private async Task<PagedResult<GiftResponseDto>> GetAllCoreAsync(
        GiftQueryParams queryParams,
        SiteSettingsDto? siteSettings,
        CancellationToken cancellationToken)
    {
        if (queryParams.Page < 1)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE);

        if (queryParams.PageSize < 1 || queryParams.PageSize > 100)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE_SIZE);

        ValidateCategory(queryParams.Category, allowEmpty: true);

        IQueryable<Gift> query = giftRepository.QueryWithContributions();

        if (siteSettings?.ShowGiftCategories == true)
        {
            if (!string.IsNullOrWhiteSpace(queryParams.Category) &&
                !siteSettings.EnabledCategories.Contains(queryParams.Category, StringComparer.Ordinal))
            {
                return new PagedResult<GiftResponseDto>
                {
                    Items = [],
                    TotalCount = 0,
                    Page = queryParams.Page,
                    PageSize = queryParams.PageSize
                };
            }

            query = query.Where(x => string.IsNullOrEmpty(x.Category) || siteSettings.EnabledCategories.Contains(x.Category));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Category))
            query = query.Where(x => x.Category == queryParams.Category);

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
            query = query.Where(x => x.Name.Contains(queryParams.Search) || x.Description.Contains(queryParams.Search));

        if (queryParams.MinTotal.HasValue)
            query = query.Where(x => x.Total >= queryParams.MinTotal.Value);

        if (queryParams.MaxTotal.HasValue)
            query = query.Where(x => x.Total <= queryParams.MaxTotal.Value);

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

        Dictionary<Guid, decimal> reservedAmounts = await GetActiveReservationAmountsAsync(
            items.Select(x => x.Id).ToArray(),
            cancellationToken);

        return new PagedResult<GiftResponseDto>
        {
            Items = items.Select(g => g.ToResponseDto(
                reservedAmounts.GetValueOrDefault(g.Id),
                siteSettings?.ShowGiftCategories ?? true)).ToList(),
            TotalCount = totalCount,
            Page = queryParams.Page,
            PageSize = queryParams.PageSize
        };
    }

    public async Task<GiftStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        string cacheKey = $"gifts:stats:{cacheService.CurrentVersion}";
        GiftStatsDto? cached = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
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
        });

        return cached!;
    }

    public async Task<GiftResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdWithContributionsAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        decimal reservedAmount = await GetActiveReservationAmountAsync(id, cancellationToken);
        return entity.ToResponseDto(reservedAmount);
    }

    public async Task<GiftResponseDto> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken)
    {
        ValidateCategory(dto.Category, allowEmpty: true);

        Gift entity = dto.ToEntity();
        await giftRepository.AddAsync(entity, cancellationToken);
        await giftRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        ValidateCategory(dto.Category, allowEmpty: true);

        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.ApplyUpdate(dto);
        await giftRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAvailabilityAsync(Guid id, bool available, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        entity.SetAvailability(available);
        await giftRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        giftRepository.Delete(entity);
        await giftRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
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

        if (!gift.Available && !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
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
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    private async Task<decimal> GetActiveReservationAmountAsync(Guid giftId, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        return await paymentRepository.Query()
            .Where(x => x.GiftId == giftId &&
                        !x.ContributionCreated &&
                        x.ExpiresAt > now &&
                        PaymentStatuses.Reserving.Contains(x.Status))
            .SumAsync(x => x.Amount, cancellationToken);
    }

    private async Task<Dictionary<Guid, decimal>> GetActiveReservationAmountsAsync(
        IReadOnlyCollection<Guid> giftIds,
        CancellationToken cancellationToken)
    {
        if (giftIds.Count == 0)
            return [];

        DateTime now = DateTime.UtcNow;

        return await paymentRepository.Query()
            .Where(x => giftIds.Contains(x.GiftId) &&
                        !x.ContributionCreated &&
                        x.ExpiresAt > now &&
                        PaymentStatuses.Reserving.Contains(x.Status))
            .GroupBy(x => x.GiftId)
            .Select(x => new { GiftId = x.Key, Amount = x.Sum(payment => payment.Amount) })
            .ToDictionaryAsync(x => x.GiftId, x => x.Amount, cancellationToken);
    }

    private static void ValidateCategory(string? category, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            if (allowEmpty)
                return;

            throw new BadRequestException(ErrorCodes.INVALID_GIFT_CATEGORY);
        }

        if (!GiftCategories.IsValid(category))
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_CATEGORY);
    }

    private async Task<bool> CoupleAllowsUnlimitedPurchasesAsync(CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        return GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);
    }
}
