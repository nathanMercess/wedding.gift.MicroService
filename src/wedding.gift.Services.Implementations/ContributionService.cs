using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class ContributionService(
    IContributionRepository contributionRepository,
    IGiftRepository giftRepository,
    ICoupleRepository coupleRepository,
    IApplicationCacheService cacheService) : IContributionService
{
    public async Task<IReadOnlyList<ContributionResponseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Contribution> contributions = await contributionRepository.GetAllAsync(cancellationToken);
        return contributions.Select(x => x.ToResponseDto()).ToList();
    }

    public async Task<ContributionResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        return entity.ToResponseDto();
    }

    public async Task<ContributionResponseDto> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(dto.Status))
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);

        Gift gift = await giftRepository.GetByIdAsync(dto.GiftId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        if (!gift.Available && !await CoupleAllowsUnlimitedPurchasesAsync(cancellationToken))
            throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);

        Contribution entity = dto.ToEntity();

        if (entity.Status == ContributionStatus.Paid)
            entity.UpdateStatus(ContributionStatus.Paid, dto.PaidAt);

        await contributionRepository.AddAsync(entity, cancellationToken);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();

        return entity.ToResponseDto();
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(status))
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);

        Contribution entity = await contributionRepository.GetByIdAsync(id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        entity.UpdateStatus(status, paidAt);
        await contributionRepository.SaveChangesAsync(cancellationToken);
        cacheService.Invalidate();
    }

    private async Task<bool> CoupleAllowsUnlimitedPurchasesAsync(CancellationToken cancellationToken)
    {
        Couple? couple = await coupleRepository.GetAsync(false, cancellationToken);
        return GiftDisplayModes.AllowsUnlimitedPurchases(couple?.GiftDisplayMode);
    }
}
