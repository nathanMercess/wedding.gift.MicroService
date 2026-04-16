using wedding.gift.Application.Webapi.Models.DTOs;
using wedding.gift.Application.Webapi.Models.Entities;

namespace wedding.gift.Application.Webapi.Services;

public interface IGiftService
{
    Task<IReadOnlyList<Gift>> GetAllAsync(string? category, bool? available, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<Gift> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Gift> CreateAsync(GiftCreateDto dto, CancellationToken cancellationToken);
    Task<Gift> UpdateAsync(Guid id, GiftUpdateDto dto, CancellationToken cancellationToken);
    Task<Gift> UpdateAvailabilityAsync(Guid id, bool available, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Contribution>> GetContributionsByGiftIdAsync(Guid giftId, CancellationToken cancellationToken);
}
