using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Services.Contracts;

public interface IAuthService
{
    Task RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken);
    Task<AuthUserResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken);
    Task ResendConfirmationEmailAsync(ResendConfirmationEmailRequestDto dto, CancellationToken cancellationToken);
}
