using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using wedding.gift.Infra.Implementations.DataContext;
using Xunit;

namespace wedding.gift.Tests;

public sealed class ApiContractTests : IClassFixture<ApiContractTests.ApiFactory>
{
    private readonly HttpClient _client;

    public ApiContractTests(ApiFactory factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task GetGifts_DeveRetornarEnvelopePadronizado()
    {
        HttpResponseMessage response = await _client.GetAsync("/api/gifts");
        JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.RootElement.GetProperty("success").GetBoolean());
        Assert.True(document.RootElement.TryGetProperty("correlationId", out _));
        Assert.True(document.RootElement.GetProperty("data").TryGetProperty("items", out _));
    }

    [Fact]
    public async Task CreatePix_DeveRetornarErroDeValidacaoPadronizado()
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/payment/pix", new { });
        JsonDocument document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(document.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", document.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task HealthLive_DeveEstarDisponivel()
    {
        HttpResponseMessage response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrderLookupRequest_DeveRetornarRespostaNeutra()
    {
        HttpResponseMessage first = await _client.PostAsJsonAsync("/api/payment/order-lookup/request", new
        {
            email = "primeiro@example.com",
            orderId = Guid.NewGuid().ToString()
        });
        HttpResponseMessage second = await _client.PostAsJsonAsync("/api/payment/order-lookup/request", new
        {
            email = "segundo@example.com",
            orderId = Guid.NewGuid().ToString()
        });
        JsonDocument firstBody = await JsonDocument.ParseAsync(await first.Content.ReadAsStreamAsync());
        JsonDocument secondBody = await JsonDocument.ParseAsync(await second.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.True(firstBody.RootElement.GetProperty("data").GetProperty("accepted").GetBoolean());
        Assert.True(secondBody.RootElement.GetProperty("data").GetProperty("accepted").GetBoolean());
    }


    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public ApiFactory()
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=test;Database=test;User Id=test;Password=test;TrustServerCertificate=True");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "wedding-gift-tests");
            Environment.SetEnvironmentVariable("Jwt__Audience", "wedding-gift-tests");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", "TEST_SIGNING_KEY_WITH_AT_LEAST_32_CHARACTERS");
            Environment.SetEnvironmentVariable("Gcs__BucketName", "test-bucket");
            Environment.SetEnvironmentVariable("MercadoPago__AccessToken", "test-token");
            Environment.SetEnvironmentVariable("MercadoPago__WebhookSecret", "TEST_WEBHOOK_SECRET_WITH_32_CHARACTERS");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=test;Database=test;User Id=test;Password=test;TrustServerCertificate=True",
                    ["Jwt:Issuer"] = "wedding-gift-tests",
                    ["Jwt:Audience"] = "wedding-gift-tests",
                    ["Jwt:SigningKey"] = "TEST_SIGNING_KEY_WITH_AT_LEAST_32_CHARACTERS",
                    ["Gcs:BucketName"] = "test-bucket",
                    ["MercadoPago:AccessToken"] = "test-token",
                    ["MercadoPago:BaseUrl"] = "https://api.mercadopago.com",
                    ["MercadoPago:WebhookSecret"] = "TEST_WEBHOOK_SECRET_WITH_32_CHARACTERS"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase($"api-tests-{Guid.NewGuid()}"));
            });
        }
    }
}
