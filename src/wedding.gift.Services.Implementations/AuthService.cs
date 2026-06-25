using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Security;

namespace wedding.gift.Services.Implementations;

public sealed class AuthService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions, IEmailService emailService, ILogger<AuthService>? logger = null) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException(ErrorCodes.USER_INACTIVE);
        }

        if (!user.IsEmailConfirmed)
        {
            throw new UnauthorizedException(ErrorCodes.EMAIL_NOT_CONFIRMED);
        }

        var isPasswordValid = PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt);

        if (!isPasswordValid)
        {
            throw new UnauthorizedException(ErrorCodes.INVALID_CREDENTIALS);
        }

        ValidateJwtConfiguration(_jwtOptions);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            ExpiresAtUtc = expiresAtUtc,
            UserName = user.Name,
            Email = user.Email,
            Role = user.Role
        };
    }

    public async Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new BadRequestException(ErrorCodes.REQUIRED_FIELDS);
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Users
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new ConflictException(ErrorCodes.EMAIL_ALREADY_EXISTS);
        }

        var (hash, salt) = PasswordHasher.HashPassword(dto.Password);
        var confirmationToken = GenerateSecureToken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Email = dto.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Member,
            IsActive = true,
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Conta já persistida. Se o e-mail falhar, NÃO derruba o cadastro (evita 500 + usuário órfão):
        // loga e segue — o usuário pode solicitar reenvio depois.
        try
        {
            await emailService.SendEmailConfirmationAsync(user.Email, user.Name, confirmationToken, cancellationToken);
        }
        catch (EmailDeliveryException ex)
        {
            logger?.LogError(ex, "Usuário {UserId} criado, mas o e-mail de confirmação falhou.", user.Id);
        }

        return new RegisterResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
        {
            throw new BadRequestException(ErrorCodes.REQUIRED_FIELDS);
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException(ErrorCodes.USER_NOT_FOUND);
        }

        if (user.IsEmailConfirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(user.EmailConfirmationToken) ||
            user.EmailConfirmationToken != dto.Token ||
            user.EmailConfirmationTokenExpiresAt is null ||
            user.EmailConfirmationTokenExpiresAt < DateTime.UtcNow)
        {
            throw new BadRequestException(ErrorCodes.INVALID_CONFIRMATION_TOKEN);
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        user.EmailConfirmationTokenExpiresAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
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
