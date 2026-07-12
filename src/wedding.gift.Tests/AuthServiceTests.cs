#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Crosscutting.Models.DTOs.Auth;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Security;
using Xunit;

namespace wedding.gift.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_DeveRetornarToken_QuandoCredenciaisValidas()
    {
        AppDbContext context = CreateContext();
        User user = CreateUser("Admin", "admin@weddinggift.com", UserRoles.Admin, true);
        context.Users.Add(user);
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        LoginResponseDto result = await service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("admin@weddinggift.com", result.Email);
        Assert.Equal(UserRoles.Admin, result.Role);

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "admin@weddinggift.com");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == UserRoles.Admin);
    }

    [Fact]
    public async Task LoginAsync_DeveRetornarRoleSuperAdmin_QuandoUsuarioForSuperAdmin()
    {
        AppDbContext context = CreateContext();
        context.Users.Add(CreateUser("Super Admin", "super-admin@weddinggift.com", UserRoles.SuperAdmin, true));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        LoginResponseDto result = await service.LoginAsync(new LoginRequestDto
        {
            Email = "super-admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.Equal(UserRoles.SuperAdmin, result.Role);

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == UserRoles.SuperAdmin);
    }

    [Fact]
    public async Task LoginAsync_DeveLancarUnauthorized_QuandoSenhaInvalida()
    {
        AppDbContext context = CreateContext();
        context.Users.Add(CreateUser("Admin", "admin@weddinggift.com", UserRoles.Admin, true));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaErrada"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.INVALID_CREDENTIALS, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_DeveLancarUnauthorized_QuandoUsuarioInativo()
    {
        AppDbContext context = CreateContext();
        User user = CreateUser("Admin", "admin@weddinggift.com", UserRoles.Admin, true);
        user.Deactivate();
        context.Users.Add(user);
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.USER_INACTIVE, ex.Code);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_ThrowsUnauthorized()
    {
        AppDbContext context = CreateContext();
        context.Users.Add(CreateUser("Novo", "novo@weddinggift.com", UserRoles.Member, false));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        UnauthorizedException ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "novo@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.EMAIL_NOT_CONFIRMED, ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_Success_SendsConfirmationEmail()
    {
        AppDbContext context = CreateContext();
        FakeEmailService emailService = new();
        AuthService service = CreateService(context, emailService);

        RegisterResponseDto result = await service.RegisterAsync(new RegisterRequestDto
        {
            Name = "Maria",
            Email = "maria@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        Assert.Equal("maria@weddinggift.com", result.Email);
        Assert.Equal("Maria", result.Name);
        Assert.True(emailService.WasCalled);
        Assert.Equal("maria@weddinggift.com", emailService.LastToEmail);

        User user = await context.Users.FirstAsync(u => u.NormalizedEmail == "maria@weddinggift.com");
        Assert.False(user.IsEmailConfirmed);
        Assert.NotNull(user.EmailConfirmationToken);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        AppDbContext context = CreateContext();
        context.Users.Add(CreateUser("Existente", "existente@weddinggift.com", UserRoles.Member, true));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

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
        AppDbContext context = CreateContext();
        string token = "token-valido-123";
        context.Users.Add(CreateUser("Pending", "pending@weddinggift.com", UserRoles.Member, false, token, DateTime.UtcNow.AddHours(24)));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        await service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            Email = "pending@weddinggift.com",
            Token = token
        }, CancellationToken.None);

        User user = await context.Users.FirstAsync(u => u.NormalizedEmail == "pending@weddinggift.com");
        Assert.True(user.IsEmailConfirmed);
        Assert.Null(user.EmailConfirmationToken);
        Assert.Null(user.EmailConfirmationTokenExpiresAt);
    }

    [Fact]
    public async Task ConfirmEmail_ExpiredToken_ThrowsBadRequest()
    {
        AppDbContext context = CreateContext();
        string token = "token-expirado";
        context.Users.Add(CreateUser("Expired", "expired@weddinggift.com", UserRoles.Member, false, token, DateTime.UtcNow.AddHours(-1)));
        await context.SaveChangesAsync(CancellationToken.None);

        AuthService service = CreateService(context);

        BadRequestException ex = await Assert.ThrowsAsync<BadRequestException>(() => service.ConfirmEmailAsync(new ConfirmEmailRequestDto
        {
            Email = "expired@weddinggift.com",
            Token = token
        }, CancellationToken.None));

        Assert.Equal(ErrorCodes.INVALID_CONFIRMATION_TOKEN, ex.Code);
    }

    [Fact]
    public async Task RefreshAsync_DeveRotacionarRefreshTokenERevogarAnterior()
    {
        AppDbContext context = CreateContext();
        context.Users.Add(CreateUser("Admin", "admin@weddinggift.com", UserRoles.Admin, true));
        await context.SaveChangesAsync();
        AuthService service = CreateService(context);
        LoginResponseDto login = await service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None);

        LoginResponseDto refreshed = await service.RefreshAsync(new RefreshTokenRequestDto
        {
            RefreshToken = login.RefreshToken
        }, CancellationToken.None);

        Assert.NotEmpty(refreshed.RefreshToken);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedException>(() => service.RefreshAsync(new RefreshTokenRequestDto
        {
            RefreshToken = login.RefreshToken
        }, CancellationToken.None));
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AuthService CreateService(AppDbContext context, IEmailService? emailService = null)
    {
        IOptions<JwtOptions> jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "wedding.gift.api",
            Audience = "wedding.gift.clients",
            SigningKey = "THIS_IS_A_TEST_KEY_WITH_32_CHARS_MINIMUM",
            AccessTokenExpirationMinutes = 60
        });

        return new AuthService(
            new UserRepository(context),
            jwtOptions,
            emailService ?? new FakeEmailService(),
            refreshTokenRepository: new RefreshTokenRepository(context));
    }

    private static User CreateUser(
        string name,
        string email,
        string role,
        bool confirmed,
        string? confirmationToken = null,
        DateTime? confirmationTokenExpiresAt = null)
    {
        (string hash, string salt) = PasswordHasher.HashPassword("SenhaForte123!");

        return User.Create(
            name,
            email,
            email.Trim().ToLowerInvariant(),
            hash,
            salt,
            role,
            confirmed,
            confirmationToken,
            confirmationTokenExpiresAt);
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool WasCalled { get; private set; }
        public string? LastToEmail { get; private set; }

        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken)
        {
            WasCalled = true;
            LastToEmail = toEmail;
            return Task.CompletedTask;
        }

        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendGuestReceiptAsync(
            string toEmail,
            string contributorName,
            string giftName,
            string orderId,
            decimal amount,
            CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
