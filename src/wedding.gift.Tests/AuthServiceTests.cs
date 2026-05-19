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
            IsActive = true
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
            IsActive = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaErrada"
        }, CancellationToken.None));

        Assert.Equal("Credenciais inválidas.", ex.Message);
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
            IsActive = false
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => service.LoginAsync(new LoginRequestDto
        {
            Email = "admin@weddinggift.com",
            Password = "SenhaForte123!"
        }, CancellationToken.None));

        Assert.Equal("Usuário inativo.", ex.Message);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AuthService CreateService(AppDbContext context)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "wedding.gift.api",
            Audience = "wedding.gift.clients",
            SigningKey = "THIS_IS_A_TEST_KEY_WITH_32_CHARS_MINIMUM",
            AccessTokenExpirationMinutes = 60
        });

        return new AuthService(context, options);
    }
}
