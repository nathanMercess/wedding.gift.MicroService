using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface ICoupleOverviewService
{
    Task<CoupleOverviewDto> GetAsync(int days, CancellationToken cancellationToken);
}
