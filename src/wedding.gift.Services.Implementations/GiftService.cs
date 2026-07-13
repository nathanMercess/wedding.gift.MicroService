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
    IApplicationCacheService cacheService,
    IRequestContext? requestContext = null,
    IOperationalRepository? operationalRepository = null) : IGiftService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(15);

    public async Task<PagedResult<GiftResponseDto>> GetPublicAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        SiteSettingsDto settings = SiteSettingsExtensions.Normalize(couple?.SiteSettingsJson);
        bool allowsUnlimitedPurchases = GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);

        return await GetAllCoreAsync(queryParams, settings, allowsUnlimitedPurchases, Couple.SingletonId, cancellationToken);
    }

    public async Task<PagedResult<GiftResponseDto>> GetAllAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
        => await GetPublicAsync(queryParams, cancellationToken);

    public async Task<PagedResult<GiftResponseDto>> GetAllAdminAsync(GiftQueryParams queryParams, CancellationToken cancellationToken)
        => await GetAllCoreAsync(queryParams, null, false, GetAdministrativeCoupleId(), cancellationToken);

    private async Task<PagedResult<GiftResponseDto>> GetAllCoreAsync(
        GiftQueryParams queryParams,
        SiteSettingsDto? siteSettings,
        bool allowsUnlimitedPurchases,
        Guid? coupleId,
        CancellationToken cancellationToken)
    {
        if (queryParams.Page < 1)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE);

        if (queryParams.PageSize < 1 || queryParams.PageSize > 100)
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PAGE_SIZE);

        if (queryParams.MinTotal.HasValue && queryParams.MaxTotal.HasValue &&
            queryParams.MinTotal.Value > queryParams.MaxTotal.Value)
        {
            throw new BadRequestException(ErrorCodes.INVALID_GIFT_PRICE_RANGE);
        }

        ValidateCategory(queryParams.Category, allowEmpty: true);

        IQueryable<Gift> query = giftRepository.QueryWithContributions();
        if (coupleId.HasValue)
            query = query.Where(x => x.CoupleId == coupleId.Value);

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

        DateTime now = DateTime.UtcNow;
        IQueryable<Payment> activeReservations = paymentRepository.Query()
            .Where(x => (!coupleId.HasValue || x.CoupleId == coupleId.Value) &&
                        !x.ContributionCreated &&
                        x.ExpiresAt > now &&
                        PaymentStatuses.Reserving.Contains(x.Status));

        if (queryParams.OnlyAvailable.HasValue && !allowsUnlimitedPurchases)
        {
            query = queryParams.OnlyAvailable.Value
                ? query.Where(x => x.Total >
                    (x.Contributions.Where(c => c.Status == ContributionStatus.Paid).Sum(c => (decimal?)(c.Amount - c.RefundedAmount)) ?? 0) +
                    (activeReservations.Where(p => p.GiftId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0))
                : query.Where(x => x.Total <=
                    (x.Contributions.Where(c => c.Status == ContributionStatus.Paid).Sum(c => (decimal?)(c.Amount - c.RefundedAmount)) ?? 0) +
                    (activeReservations.Where(p => p.GiftId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0));
        }

        if (queryParams.OnlyAvailable == false && allowsUnlimitedPurchases)
        {
            return new PagedResult<GiftResponseDto>
            {
                Items = [],
                TotalCount = 0,
                Page = queryParams.Page,
                PageSize = queryParams.PageSize
            };
        }

        query = (queryParams.OrderBy, queryParams.OrderDir) switch
        {
            (GiftSortField.Price, SortDirection.Desc) => query.OrderByDescending(x => x.Price).ThenBy(x => x.Name),
            (GiftSortField.Price, _) => query.OrderBy(x => x.Price).ThenBy(x => x.Name),

            (GiftSortField.Total, SortDirection.Desc) => query.OrderByDescending(x => x.Total).ThenBy(x => x.Name),
            (GiftSortField.Total, _) => query.OrderBy(x => x.Total).ThenBy(x => x.Name),

            (GiftSortField.Raised, SortDirection.Desc) => query
                .OrderByDescending(x => x.Contributions
                    .Where(c => c.Status == ContributionStatus.Paid)
                    .Sum(c => c.Amount - c.RefundedAmount))
                .ThenBy(x => x.Name),
            (GiftSortField.Raised, _) => query
                .OrderBy(x => x.Contributions
                    .Where(c => c.Status == ContributionStatus.Paid)
                    .Sum(c => c.Amount - c.RefundedAmount))
                .ThenBy(x => x.Name),

            (GiftSortField.Available, SortDirection.Desc) => query
                .OrderByDescending(x => x.Total >
                    (x.Contributions.Where(c => c.Status == ContributionStatus.Paid).Sum(c => (decimal?)(c.Amount - c.RefundedAmount)) ?? 0) +
                    (activeReservations.Where(p => p.GiftId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0))
                .ThenBy(x => x.Name),
            (GiftSortField.Available, _) => query
                .OrderBy(x => x.Total >
                    (x.Contributions.Where(c => c.Status == ContributionStatus.Paid).Sum(c => (decimal?)(c.Amount - c.RefundedAmount)) ?? 0) +
                    (activeReservations.Where(p => p.GiftId == x.Id).Sum(p => (decimal?)p.Amount) ?? 0))
                .ThenBy(x => x.Name),

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
                siteSettings?.ShowGiftCategories ?? true,
                allowsUnlimitedPurchases)).ToList(),
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

        EnsureCoupleAccess(entity.CoupleId);
        decimal reservedAmount = await GetActiveReservationAmountAsync(id, cancellationToken);
        bool allowsUnlimitedPurchases = await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken);
        return entity.ToResponseDto(reservedAmount, allowsUnlimitedPurchases: allowsUnlimitedPurchases);
    }

    public async Task<GiftResponseDto> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken)
    {
        ValidateCategory(dto.Category, allowEmpty: true);

        Gift entity = dto.ToEntity(GetRequiredCoupleId());
        await giftRepository.AddAsync(entity, cancellationToken);
        await AddAuditAsync("GiftCreated", entity, cancellationToken);
        await giftRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
        return entity.ToResponseDto();
    }

    public async Task<GiftResponseDto> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken)
    {
        ValidateCategory(dto.Category, allowEmpty: true);

        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        EnsureCoupleAccess(entity.CoupleId);
        entity.ApplyUpdate(dto);
        await AddAuditAsync("GiftUpdated", entity, cancellationToken);
        await SaveGiftChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    public async Task<GiftCategoryBatchUpdateResponseDto> UpdateCategoriesAsync(GiftCategoryBatchUpdateDto dto, CancellationToken cancellationToken)
    {
        ValidateCategory(dto.Category, allowEmpty: true);
        Guid[] giftIds = dto.GiftIds.Distinct().ToArray();

        if (giftIds.Length == 0)
            throw new BadRequestException(ErrorCodes.BAD_REQUEST);

        IReadOnlyList<Gift> gifts = await giftRepository.GetByIdsAsync(giftIds, GetAdministrativeCoupleId(), cancellationToken);

        if (gifts.Count != giftIds.Length)
            throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        foreach (Gift gift in gifts)
        {
            gift.UpdateCategory(dto.Category);
            await AddAuditAsync("GiftCategoryUpdated", gift, cancellationToken);
        }

        await SaveGiftChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return new GiftCategoryBatchUpdateResponseDto
        {
            GiftIds = [.. giftIds],
            Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim()
        };
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Gift entity = await giftRepository.GetByIdAsync(id, cancellationToken)
                      ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        EnsureCoupleAccess(entity.CoupleId);
        await AddAuditAsync("GiftDeleted", entity, cancellationToken);
        bool hasFinancialHistory = await contributionRepository.Query()
            .AnyAsync(x => x.GiftId == id, cancellationToken) ||
            await paymentRepository.Query().AnyAsync(x => x.GiftId == id, cancellationToken);

        if (hasFinancialHistory)
            throw new ConflictException(ErrorCodes.GIFT_HAS_FINANCIAL_HISTORY);

        giftRepository.Delete(entity);
        await SaveGiftChangesAsync(cancellationToken);
        cacheService.Invalidate();
    }

    public async Task<IReadOnlyList<ContributionResponseDto>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken)
    {
        Gift gift = await giftRepository.GetByIdAsync(giftId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
        EnsureCoupleAccess(gift.CoupleId);

        IReadOnlyList<Contribution> contributions = await contributionRepository.GetByGiftIdAsync(giftId, cancellationToken);
        return contributions
            .Where(x => x.Status == ContributionStatus.Paid)
            .Take(100)
            .Select(x => x.ToPublicResponseDto())
            .ToList();
    }

    public async Task<ContributionResponseDto> ContributeAsync(Guid giftId, ContributeDto dto, CancellationToken cancellationToken)
    {
        Gift gift = await giftRepository.GetByIdWithContributionsAsync(giftId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        EnsureCoupleAccess(gift.CoupleId);
        if (gift.RemainingAmount <= 0 && !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
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

    private async Task SaveGiftChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await giftRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ErrorCodes.CONCURRENT_MODIFICATION);
        }
    }

    private Guid? GetAdministrativeCoupleId()
        => requestContext?.IsSuperAdmin == true ? null : GetRequiredCoupleId();

    private Guid GetRequiredCoupleId()
        => requestContext?.CoupleId ?? Couple.SingletonId;

    private void EnsureCoupleAccess(Guid coupleId)
    {
        if (requestContext?.IsSuperAdmin == true)
            return;

        if (coupleId != GetRequiredCoupleId())
            throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);
    }

    private async Task AddAuditAsync(string action, Gift gift, CancellationToken cancellationToken)
    {
        if (operationalRepository is null)
            return;

        await operationalRepository.AddAuditLogAsync(
            AuditLog.Create(requestContext?.UserId, gift.CoupleId, action, "Gift", gift.Id.ToString(), requestContext?.CorrelationId ?? string.Empty),
            cancellationToken);
    }
}
