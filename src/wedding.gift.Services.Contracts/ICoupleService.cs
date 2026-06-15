using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Contracts;

public interface ICoupleService
{
    Task<Couple?> GetAsync(CancellationToken cancellationToken);
    Task<Couple> UpdateAsync(CoupleUpdateDto dto, CancellationToken cancellationToken);
}
