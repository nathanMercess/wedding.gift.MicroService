using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IContributionService
{
    Task<IReadOnlyList<ContributionResponseDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<ContributionResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ContributionResponseDto> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken);
    Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken);
}
