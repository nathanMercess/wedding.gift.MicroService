using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IContributionService
{
    Task<PagedResult<ContributionResponseDto>> GetAllAsync(ContributionQueryParams query, CancellationToken cancellationToken);
    Task<PagedResult<ContributionResponseDto>> GetAllAdminAsync(ContributionAdminQueryParams query, CancellationToken cancellationToken);
    Task<ContributionResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ContributionResponseDto> CreateAsync(ContributionCreateDto dto, CancellationToken cancellationToken);
    Task UpdateStatusAsync(Guid id, string status, DateTime paidAt, CancellationToken cancellationToken);
    Task<ContributionResponseDto> SetMessageReadAsync(Guid id, bool read, CancellationToken cancellationToken);
    Task<ContributionResponseDto> SetMessageArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken);
    Task<byte[]> ExportCsvAsync(ContributionAdminQueryParams query, CancellationToken cancellationToken);
}
