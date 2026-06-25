using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
    ILogger<AuthService>? logger = null) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

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

        ValidateJwtConfiguration(_jwtOptions);

        DateTime expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims = new()
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        JwtSecurityToken token = new(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        string accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return user.ToLoginResponseDto(accessToken, expiresAtUtc);
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
            confirmationToken,
            DateTime.UtcNow.AddHours(24));

        await userRepository.AddAsync(user, cancellationToken);
        await userRepository.SaveChangesAsync(cancellationToken);

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
            user.EmailConfirmationToken != dto.Token ||
            user.EmailConfirmationTokenExpiresAt is null ||
            user.EmailConfirmationTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BadRequestException(ErrorCodes.INVALID_CONFIRMATION_TOKEN);
        }

        user.ConfirmEmail();
        await userRepository.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateSecureToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
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
