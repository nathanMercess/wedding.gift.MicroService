using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Contracts;

public interface IContributionService
{
    Task<IReadOnlyList<Contribution>> GetAllAsync(CancellationToken cancellationToken);
    Task<Contribution> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Contribution> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken);
    Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken);
}
