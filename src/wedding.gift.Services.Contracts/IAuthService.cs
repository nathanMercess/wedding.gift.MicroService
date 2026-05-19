using wedding.gift.Crosscutting.Models.DTOs.Auth;

namespace wedding.gift.Services.Contracts;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken);
}
