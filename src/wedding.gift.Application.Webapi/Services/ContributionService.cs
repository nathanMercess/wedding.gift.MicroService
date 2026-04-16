using Microsoft.EntityFrameworkCore;
using wedding.gift.Application.Webapi.Data;
using wedding.gift.Application.Webapi.Mappings;
using wedding.gift.Application.Webapi.Models.DTOs;
using wedding.gift.Application.Webapi.Models.Entities;
using wedding.gift.Application.Webapi.Services.Exceptions;

namespace wedding.gift.Application.Webapi.Services;

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
            gift.Available = false;
            gift.UpdatedAt = DateTime.UtcNow;
            entity.PaidAt = dto.PaidAt == default ? DateTime.UtcNow : dto.PaidAt;
        }

        dbContext.Contributions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
