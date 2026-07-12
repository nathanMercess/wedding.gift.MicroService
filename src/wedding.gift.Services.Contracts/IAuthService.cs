using wedding.gift.Crosscutting.Models.DTOs.Auth;

namespace wedding.gift.Services.Contracts;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken);
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken);
    Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken);
    Task ResendConfirmationAsync(EmailRequestDto dto, CancellationToken cancellationToken);
    Task ForgotPasswordAsync(EmailRequestDto dto, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken);
    Task<LoginResponseDto> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken);
    Task LogoutAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken);
    Task<UserResponseDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequestDto dto, CancellationToken cancellationToken);
}
