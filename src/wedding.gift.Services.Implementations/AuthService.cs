using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public class AuthService(AppDbContext dbContext, IEmailSender emailSender) : IAuthService
{
    private const int PasswordIterations = 100_000;
    private static readonly TimeSpan EmailTokenLifetime = TimeSpan.FromMinutes(30);

    public async Task RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var emailAlreadyExists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyExists)
        {
            throw new ConflictException("Já existe um usuário cadastrado com este e-mail.");
        }

        CreatePasswordHash(dto.Password, out var passwordHash, out var passwordSalt);
        var plainToken = GenerateEmailToken();
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            IsEmailConfirmed = false,
            EmailConfirmationTokenHash = ComputeTokenHash(plainToken),
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.Add(EmailTokenLifetime)
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await SendEmailConfirmationTokenAsync(user.Email, plainToken, cancellationToken);
    }

    public async Task<AuthUserResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new BadRequestException("Credenciais inválidas.");
        }

        if (!user.IsEmailConfirmed)
        {
            throw new ConflictException("Seu e-mail ainda não foi confirmado.");
        }

        return new AuthUserResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            IsEmailConfirmed = user.IsEmailConfirmed
        };
    }

    public async Task ConfirmEmailAsync(ConfirmEmailRequestDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            throw new BadRequestException("Token inválido ou expirado.");
        }

        if (user.IsEmailConfirmed)
        {
            return;
        }

        var tokenHash = ComputeTokenHash(dto.Token);
        var tokenExpiresAt = user.EmailConfirmationTokenExpiresAt;
        var tokenIsValid = !string.IsNullOrWhiteSpace(user.EmailConfirmationTokenHash)
                           && CryptographicOperations.FixedTimeEquals(
                               Encoding.UTF8.GetBytes(user.EmailConfirmationTokenHash),
                               Encoding.UTF8.GetBytes(tokenHash))
                           && tokenExpiresAt.HasValue
                           && tokenExpiresAt.Value >= DateTime.UtcNow;

        if (!tokenIsValid)
        {
            throw new BadRequestException("Token inválido ou expirado.");
        }

        user.IsEmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;
        user.EmailConfirmationTokenHash = string.Empty;
        user.EmailConfirmationTokenExpiresAt = null;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResendConfirmationEmailAsync(ResendConfirmationEmailRequestDto dto, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(dto.Email);
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || user.IsEmailConfirmed)
        {
            return;
        }

        var plainToken = GenerateEmailToken();
        user.EmailConfirmationTokenHash = ComputeTokenHash(plainToken);
        user.EmailConfirmationTokenExpiresAt = DateTime.UtcNow.Add(EmailTokenLifetime);

        await dbContext.SaveChangesAsync(cancellationToken);
        await SendEmailConfirmationTokenAsync(user.Email, plainToken, cancellationToken);
    }

    private async Task SendEmailConfirmationTokenAsync(string to, string token, CancellationToken cancellationToken)
    {
        const string subject = "Confirmação de e-mail - Wedding Gift";
        var htmlBody = $"""
                        <p>Olá!</p>
                        <p>Use o token abaixo para confirmar seu e-mail:</p>
                        <h2 style="letter-spacing: 4px;">{token}</h2>
                        <p>Este token expira em 30 minutos.</p>
                        """;

        await emailSender.SendAsync(to, subject, htmlBody, cancellationToken);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static void CreatePasswordHash(string password, out string hash, out string salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, saltBytes, PasswordIterations, HashAlgorithmName.SHA256, 32);

        hash = Convert.ToBase64String(hashBytes);
        salt = Convert.ToBase64String(saltBytes);
    }

    private static bool VerifyPassword(string password, string expectedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        var expectedHashBytes = Convert.FromBase64String(expectedHash);
        var candidateHashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(candidateHashBytes, expectedHashBytes);
    }

    private static string GenerateEmailToken()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }

    private static string ComputeTokenHash(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token.Trim());
        var hashBytes = SHA256.HashData(tokenBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
