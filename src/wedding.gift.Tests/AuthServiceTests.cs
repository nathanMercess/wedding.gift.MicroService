using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Security;

namespace wedding.gift.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_DeveRetornarToken_QuandoCredenciaisValidas()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@weddinggift.com",
            NormalizedEmail = "admin@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Admin,
            IsActive = true,
            IsEmailConfirmed = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("admin@weddinggift.com", result.Email);
        Assert.Equal(UserRoles.Admin, result.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "admin@weddinggift.com");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == UserRoles.Admin);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarRoleSuperAdmin_QuandoUsuarioForSuperAdmin()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Super Admin",
            Email = "super-admin@weddinggift.com",
            NormalizedEmail = "super-admin@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.SuperAdmin,
            IsActive = true,
            IsEmailConfirmed = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.LoginAsync(new LoginRequestDto
        {
            Email = "super-admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.Equal(UserRoles.SuperAdmin, result.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == UserRoles.SuperAdmin);
    }

    [Fact]
    public async Task LoginAsync_DeveLancarUnauthorized_QuandoSenhaInvalida()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@weddinggift.com",
            NormalizedEmail = "admin@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Admin,
            IsActive = true,
            IsEmailConfirmed = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaErrada"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.INVALID_CREDENTIALS, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_DeveLancarUnauthorized_QuandoUsuarioInativo()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Email = "admin@weddinggift.com",
            NormalizedEmail = "admin@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Admin,
            IsActive = false,
            IsEmailConfirmed = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.USER_INACTIVE, ex.Code);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ThrowsUnauthorized()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Novo",
            Email = "novo@weddinggift.com",
            NormalizedEmail = "novo@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Member,
            IsActive = true,
            IsEmailConfirmed = false
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "novo@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.EMAIL_NOT_CONFIRMED, ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_Success_SendsConfirmationEmail()
    {
        var context = CreateContext();
        var emailService = new FakeEmailService();
        var service = CreateService(context, emailService);

        var result = await service.RegisterAsync(new RegisterRequestDto
        {
            Name = "Maria",
            Email = "maria@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.Equal("maria@weddinggift.com", result.Email);
        Assert.Equal("Maria", result.Name);
        Assert.True(emailService.WasCalled);
        Assert.Equal("maria@weddinggift.com", emailService.LastToEmail);

        var user = await context.Users.FirstAsync(u => u.NormalizedEmail == "maria@weddinggift.com");
        Assert.False(user.IsEmailConfirmed);
        Assert.NotNull(user.EmailConfirmationToken);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        var context = CreateContext();
        var (hash, salt) = PasswordHasher.HashPassword("SenhaForte123!");

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Existente",
            Email = "existente@weddinggift.com",
            NormalizedEmail = "existente@weddinggift.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRoles.Member,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<ConflictException>(() => service.RegisterAsync(new RegisterRequestDto
        {
            Name = "Outro",
            Email = "Existente@WeddingGift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task ConfirmEmail_ValidToken_ConfirmsUser()
    {
        var context = CreateContext();
        var token = "token-valido-123";

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Pending",
            Email = "pending@weddinggift.com",
            NormalizedEmail = "pending@weddinggift.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRoles.Member,
            IsActive = true,
            IsEmailConfirmed = false,
            EmailConfirmationToken = token,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            Email = "pending@weddinggift.com",
            Token = token
        }, CancellationToken.None);

        var user = await context.Users.FirstAsync(u => u.NormalizedEmail == "pending@weddinggift.com");
        Assert.True(user.IsEmailConfirmed);
        Assert.Null(user.EmailConfirmationToken);
        Assert.Null(user.EmailConfirmationTokenExpiresAt);
    }

    [Fact]
    public async Task ConfirmEmail_ExpiredToken_ThrowsBadRequest()
    {
        var context = CreateContext();
        var token = "token-expirado";

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Name = "Expired",
            Email = "expired@weddinggift.com",
            NormalizedEmail = "expired@weddinggift.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRoles.Member,
            IsActive = true,
            IsEmailConfirmed = false,
            EmailConfirmationToken = token,
            EmailConfirmationTokenExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            Email = "expired@weddinggift.com",
            Token = token
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.INVALID_CONFIRMATION_TOKEN, ex.Code);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AuthService CreateService(AppDbContext context, IEmailService? emailService = null)
    {
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "wedding.gift.api",
            Audience = "wedding.gift.clients",
            SigningKey = "THIS_IS_A_TEST_KEY_WITH_32_CHARS_MINIMUM",
            AccessTokenExpirationMinutes = 60
        });

        return new AuthService(context, jwtOptions, emailService ?? new FakeEmailService());
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool WasCalled { get; private set; }
        public string? LastToEmail { get; private set; }

        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastToEmail = toEmail;
            return Task.CompletedTask;
        }

        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
