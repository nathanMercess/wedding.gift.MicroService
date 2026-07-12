using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IGiftService
{
    Task<PagedResult<GiftResponseDto>> GetPublicAsync(GiftQueryParams query, CancellationToken cancellationToken);
    Task<PagedResult<GiftResponseDto>> GetAllAsync(GiftQueryParams query, CancellationToken cancellationToken);
    Task<PagedResult<GiftResponseDto>> GetAllAdminAsync(GiftQueryParams query, CancellationToken cancellationToken);
    Task<GiftStatsDto> GetStatsAsync(CancellationToken cancellationToken);
    Task<GiftResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GiftResponseDto> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken);
    Task<GiftResponseDto> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContributionResponseDto>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken);
    Task<ContributionResponseDto> ContributeAsync(Guid giftId, ContributeDto dto, CancellationToken cancellationToken);
}
