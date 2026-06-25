using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface ICoupleService
{
    Task<CoupleResponseDto> GetAsync(CancellationToken cancellationToken);
    Task<CoupleResponseDto> UpdateAsync(CoupleUpdateDto dto, CancellationToken cancellationToken);
}
