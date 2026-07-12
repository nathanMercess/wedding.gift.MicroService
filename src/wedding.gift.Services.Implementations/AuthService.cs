using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;
using wedding.gift.Services.Implementations.Security;

namespace wedding.gift.Services.Implementations;

public sealed class AuthService(
    IUserRepository userRepository,
    IOptions<JwtOptions> jwtOptions,
    IEmailService emailService,
    ILogger<AuthService>? logger = null,
    IRefreshTokenRepository? refreshTokenRepository = null) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);

        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        User user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, false, cancellationToken);

        if (user is null)
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);

        if (!user.IsActive)
            throw new UnauthorizedException(ErrorCodes.USER_INACTIVE);

        if (!user.IsEmailConfirmed)
            throw new UnauthorizedException(ErrorCodes.EMAIL_NOT_CONFIRMED);

        bool isPasswordValid = PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt);

        if (!isPasswordValid)
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);

        (string accessToken, DateTime expiresAtUtc) = CreateAccessToken(user);
        LoginResponseDto response = user.ToLoginResponseDto(accessToken, expiresAtUtc);
        await AddRefreshTokenAsync(user, response, cancellationToken);
        return response;
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException(ErrorCodes.REQUIRED_FIELDS);

        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        bool exists = await userRepository.ExistsByNormalizedEmailAsync(normalizedEmail, cancellationToken);

        if (exists)
            throw new ConflictException(ErrorCodes.EMAIL_ALREADY_EXISTS);

        (string hash, string salt) = PasswordHasher.HashPassword(dto.Password);
        string confirmationToken = GenerateSecureToken();

        User user = User.Create(
            dto.Name,
            dto.Email,
            normalizedEmail,
            hash,
            salt,
            UserRoles.Member,
            false,
            HashToken(confirmationToken),
            DateTime.UtcNow.AddHours(24));

        await userRepository.AddAsync(user, cancellationToken);

        try
        {
            await userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(ErrorCodes.EMAIL_ALREADY_EXISTS);
        }

        try
        {
            await emailService.SendEmailConfirmationAsync(user.Email, user.Name, confirmationToken, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger?.LogError(ex, "Usuario {UserId} criado, mas o e-mail de confirmacao falhou.", user.Id);
        }

        return user.ToRegisterResponseDto();
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
            throw new BadRequestException(ErrorCodes.REQUIRED_FIELDS);

        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        User user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, true, cancellationToken);

        if (user is null)
            throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);

        if (user.IsEmailConfirmed)
            return;

        if (string.IsNullOrWhiteSpace(user.EmailConfirmationToken) ||
            !TokenMatches(user.EmailConfirmationToken, dto.Token) ||
            user.EmailConfirmationTokenExpiresAt is null ||
            user.EmailConfirmationTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BadRequestException(ErrorCodes.INVALID_CONFIRMATION_TOKEN);
        }

        user.ConfirmEmail();
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendConfirmationAsync(EmailRequestDto dto, CancellationToken cancellationToken)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        User? user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, true, cancellationToken);

        if (user is null || user.IsEmailConfirmed)
            return;

        string token = GenerateSecureToken();
        user.SetEmailConfirmationToken(HashToken(token), DateTime.UtcNow.AddHours(24));
        await userRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendEmailConfirmationAsync(user.Email, user.Name, token, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger?.LogError(ex, "Falha ao reenviar confirmação para o usuário {UserId}.", user.Id);
        }
    }

    public async Task ForgotPasswordAsync(EmailRequestDto dto, CancellationToken cancellationToken)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        User? user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, true, cancellationToken);

        if (user is null || !user.IsActive)
            return;

        string token = GenerateSecureToken();
        user.SetPasswordResetToken(HashToken(token), DateTime.UtcNow.AddHours(1));
        await userRepository.SaveChangesAsync(cancellationToken);

        try
        {
            await emailService.SendPasswordResetAsync(user.Email, user.Name, token, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger?.LogError(ex, "Falha ao enviar redefinição de senha para o usuário {UserId}.", user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        User? user = await userRepository.GetByNormalizedEmailAsync(normalizedEmail, true, cancellationToken);

        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
            !TokenMatches(user.PasswordResetToken, dto.Token) ||
            user.PasswordResetTokenExpiresAt is null ||
            user.PasswordResetTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BadRequestException(ErrorCodes.INVALID_PASSWORD_RESET_TOKEN);
        }

        (string hash, string salt) = PasswordHasher.HashPassword(dto.Password);
        user.ResetPassword(hash, salt);

        if (refreshTokenRepository is not null)
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);

        await userRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<LoginResponseDto> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        if (refreshTokenRepository is null)
            throw new UnauthorizedException(ErrorCodes.INVALID_REFRESH_TOKEN);

        RefreshToken token = await refreshTokenRepository.GetByHashForUpdateAsync(HashToken(dto.RefreshToken), cancellationToken)
                             ?? throw new UnauthorizedException(ErrorCodes.INVALID_REFRESH_TOKEN);

        if (!token.IsActive || !token.User.IsActive || !token.User.IsEmailConfirmed)
            throw new UnauthorizedException(ErrorCodes.INVALID_REFRESH_TOKEN);

        string nextRawToken = GenerateSecureToken();
        string nextTokenHash = HashToken(nextRawToken);
        DateTime refreshExpiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime);
        token.Revoke(nextTokenHash);
        await refreshTokenRepository.AddAsync(
            RefreshToken.Create(token.UserId, nextTokenHash, refreshExpiresAtUtc),
            cancellationToken);

        (string accessToken, DateTime expiresAtUtc) = CreateAccessToken(token.User);
        LoginResponseDto response = token.User.ToLoginResponseDto(accessToken, expiresAtUtc);
        response.RefreshToken = nextRawToken;
        response.RefreshTokenExpiresAtUtc = refreshExpiresAtUtc;
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        if (refreshTokenRepository is null)
            return;

        RefreshToken? token = await refreshTokenRepository.GetByHashForUpdateAsync(HashToken(dto.RefreshToken), cancellationToken);
        if (token is null || !token.IsActive)
            return;

        token.Revoke();
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponseDto> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdAsync(userId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            IsActive = user.IsActive,
            IsEmailConfirmed = user.IsEmailConfirmed,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequestDto dto,
        CancellationToken cancellationToken)
    {
        User user = await userRepository.GetByIdAsync(userId, cancellationToken)
                    ?? throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);

        if (!PasswordHasher.VerifyPassword(dto.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            throw new BadRequestException(ErrorCodes.INVALID_CURRENT_PASSWORD);

        (string hash, string salt) = PasswordHasher.HashPassword(dto.NewPassword);
        user.ResetPassword(hash, salt);

        if (refreshTokenRepository is not null)
            await refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);

        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task AddRefreshTokenAsync(
        User user,
        LoginResponseDto response,
        CancellationToken cancellationToken)
    {
        if (refreshTokenRepository is null)
            return;

        string rawToken = GenerateSecureToken();
        DateTime expiresAtUtc = DateTime.UtcNow.Add(RefreshTokenLifetime);
        await refreshTokenRepository.AddAsync(
            RefreshToken.Create(user.Id, HashToken(rawToken), expiresAtUtc),
            cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        response.RefreshToken = rawToken;
        response.RefreshTokenExpiresAtUtc = expiresAtUtc;
    }

    private (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(User user)
    {
        ValidateJwtConfiguration(_jwtOptions);
        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        if (user.CoupleId.HasValue)
            claims.Add(new Claim("couple_id", user.CoupleId.Value.ToString()));
        JwtSecurityToken token = new(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TokenMatches(string storedToken, string providedToken)
    {
        byte[] storedBytes = Encoding.UTF8.GetBytes(storedToken);
        byte[] hashedBytes = Encoding.UTF8.GetBytes(HashToken(providedToken));

        if (storedBytes.Length == hashedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(storedBytes, hashedBytes))
        {
            return true;
        }

        byte[] providedBytes = Encoding.UTF8.GetBytes(providedToken);
        return storedBytes.Length == providedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);
    }

    private static void ValidateJwtConfiguration(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            string.IsNullOrWhiteSpace(options.SigningKey) ||
            options.SigningKey.Length < 32)
        {
            throw new BadRequestException(ErrorCodes.INVALID_JWT_CONFIGURATION);
        }
    }
}
