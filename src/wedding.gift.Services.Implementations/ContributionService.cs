using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public sealed class ContributionService(AppDbContext dbContext) : IContributionService
{
    public async Task<IReadOnlyList<ContributionResponseDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Contribution> contributions = await dbContext.Contributions
            .AsNoTracking()
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);

        return contributions.Select(x => x.ToResponseDto()).ToList();
    }

    public async Task<ContributionResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        Contribution entity = await dbContext.Contributions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                              ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        return entity.ToResponseDto();
    }

    public async Task<ContributionResponseDto> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(dto.Status))
        {
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);
        }

        var gift = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == dto.GiftId, cancellationToken)
                   ?? throw new NotFoundException(ErrorCodes.GIFT_NOT_FOUND);

        if (!gift.Available)
        {
            throw new ConflictException(ErrorCodes.GIFT_UNAVAILABLE);
        }

        var entity = dto.ToEntity();

        if (entity.Status == ContributionStatus.Paid)
        {
            entity.PaidAt = dto.PaidAt == default ? DateTime.UtcNow : dto.PaidAt;
        }

        dbContext.Contributions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity.ToResponseDto();
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(status))
        {
            throw new BadRequestException(ErrorCodes.INVALID_CONTRIBUTION_STATUS);
        }

        var entity = await dbContext.Contributions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(ErrorCodes.CONTRIBUTION_NOT_FOUND);

        var previousStatus = entity.Status;
        entity.Status = status;

        if (status == ContributionStatus.Paid && previousStatus != ContributionStatus.Paid)
        {
            entity.PaidAt = paidAt == default ? DateTime.UtcNow : paidAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

    }
}
