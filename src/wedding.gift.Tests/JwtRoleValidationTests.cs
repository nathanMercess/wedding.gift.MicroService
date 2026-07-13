using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using Xunit;

namespace wedding.gift.Tests;

public sealed class JwtRoleValidationTests : IClassFixture<ApiContractTests.ApiFactory>, IDisposable
{
    private const string Issuer = "wedding-gift-tests";
    private const string Audience = "wedding-gift-tests";
    private const string SigningKey = "TEST_SIGNING_KEY_WITH_AT_LEAST_32_CHARACTERS";
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public JwtRoleValidationTests(ApiContractTests.ApiFactory factory)
    {
        string databaseName = $"jwt-role-tests-{Guid.NewGuid()}";
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData(UserRoles.Member)]
    [InlineData(UserRoles.SuperAdmin)]
    public async Task ProtectedEndpoint_DeveAceitarTokenComRoleAtual(string role)
    {
        User user = await CreateUserAsync(role);
        string token = CreateAccessToken(user.Id, role);

        using HttpResponseMessage response = await SendProfileRequestAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRoles.SuperAdmin, UserRoles.Member)]
    [InlineData(UserRoles.Member, UserRoles.SuperAdmin)]
    public async Task ProtectedEndpoint_DeveRejeitarTokenComRoleAntiga(string tokenRole, string currentRole)
    {
        User user = await CreateUserAsync(tokenRole);
        string token = CreateAccessToken(user.Id, tokenRole);
        await UpdateRoleAsync(user.Id, currentRole);

        using HttpResponseMessage response = await SendProfileRequestAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<User> CreateUserAsync(string role)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        string uniqueValue = Guid.NewGuid().ToString("N");
        string email = $"jwt-role-{uniqueValue}@example.com";
        User user = User.Create(
            "Usuário JWT",
            email,
            email,
            "password-hash",
            "password-salt",
            role,
            true,
            null,
            null);

        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private async Task UpdateRoleAsync(Guid userId, string role)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        User user = await dbContext.Users.SingleAsync(x => x.Id == userId);

        user.SetRole(role);
        await dbContext.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> SendProfileRequestAsync(string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string CreateAccessToken(Guid userId, string role)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role)
        ];
        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
