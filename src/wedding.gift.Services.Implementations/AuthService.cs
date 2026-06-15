using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Security;

namespace wedding.gift.Services.Implementations;

public class AuthService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions, IEmailService emailService) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new UnauthorizedException("Credenciais inválidas.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedException("Credenciais inválidas.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedException("Usuário inativo.");
        }

        if (!user.IsEmailConfirmed)
        {
            throw new UnauthorizedException("E-mail não confirmado. Verifique sua caixa de entrada.");
        }

        var isPasswordValid = PasswordHasher.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt);

        if (!isPasswordValid)
        {
            throw new UnauthorizedException("Credenciais inválidas.");
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
            throw new BadRequestException("Todos os campos são obrigatórios.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Users
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new ConflictException("Já existe uma conta com este e-mail.");
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

        await emailService.SendEmailConfirmationAsync(user.Email, user.Name, confirmationToken, cancellationToken);

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
            throw new BadRequestException("E-mail e token são obrigatórios.");
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("Usuário não encontrado.");
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
            throw new BadRequestException("Token de confirmação inválido ou expirado.");
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
            throw new BadRequestException("Configuração JWT inválida.");
        }
    }
}
