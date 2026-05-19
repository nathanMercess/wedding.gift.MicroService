using wedding.gift.Crosscutting.Models.DTOs.Auth;

namespace wedding.gift.Services.Contracts;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken);
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken);
}
