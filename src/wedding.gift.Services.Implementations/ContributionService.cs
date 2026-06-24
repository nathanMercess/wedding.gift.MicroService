using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Services.Implementations;

public class ContributionService(AppDbContext dbContext) : IContributionService
{
    public async Task<IReadOnlyList<Contribution>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Contributions
            .AsNoTracking()
            .OrderByDescending(x => x.PaidAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Contribution> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Contributions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity ?? throw new NotFoundException($"Contribuição com id '{id}' não foi encontrada.");
    }

    public async Task<Contribution> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(dto.Status))
        {
            throw new BadRequestException("Status inválido. Valores permitidos: Pending, Paid, Cancelled.");
        }

        var gift = await dbContext.Gifts.FirstOrDefaultAsync(x => x.Id == dto.GiftId, cancellationToken)
                   ?? throw new NotFoundException($"Presente com id '{dto.GiftId}' não foi encontrado.");

        if (!gift.Available)
        {
            throw new ConflictException("Não é permitido contribuir para um presente indisponível.");
        }

        var entity = dto.ToEntity();

        if (entity.Status == ContributionStatus.Paid)
        {
            entity.PaidAt = dto.PaidAt == default ? DateTime.UtcNow : dto.PaidAt;
        }

        dbContext.Contributions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken)
    {
        if (!ContributionStatus.Allowed.Contains(status))
        {
            throw new BadRequestException("Status inválido. Valores permitidos: Pending, Paid, Cancelled.");
        }

        var entity = await dbContext.Contributions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException($"Contribuição com id '{id}' não foi encontrada.");

        var previousStatus = entity.Status;
        entity.Status = status;

        if (status == ContributionStatus.Paid && previousStatus != ContributionStatus.Paid)
        {
            entity.PaidAt = paidAt == default ? DateTime.UtcNow : paidAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

    }
}
