using wedding.gift.Application.Webapi.Models.DTOs;
using wedding.gift.Application.Webapi.Models.Entities;

namespace wedding.gift.Application.Webapi.Services;

public interface IContributionService
{
    Task<IReadOnlyList<Contribution>> GetAllAsync(CancellationToken cancellationToken);
    Task<Contribution> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Contribution> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken);
}
