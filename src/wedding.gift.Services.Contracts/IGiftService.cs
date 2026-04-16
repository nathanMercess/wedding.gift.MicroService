using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Contracts;

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
