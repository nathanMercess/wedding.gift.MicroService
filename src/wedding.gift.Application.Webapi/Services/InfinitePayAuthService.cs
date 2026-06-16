using System.Net.Http.Json;
using wedding.gift.Application.Webapi.Models.InfinitePay;
using wedding.gift.Application.Webapi.Services.Interfaces;

namespace wedding.gift.Application.Webapi.Services;

public class InfinitePayAuthService(HttpClient http, IConfiguration config) : IInfinitePayAuthService
{
    private string? cachedToken;
    private DateTime tokenExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim authLock = new(1, 1);

    public async Task<string> GetAccessTokenAsync(string scope, CancellationToken ct = default)
    {
        if (cachedToken != null && DateTime.UtcNow < tokenExpiry)
        {
            return cachedToken;
        }

        await authLock.WaitAsync(ct);
        try
        {
            if (cachedToken != null && DateTime.UtcNow < tokenExpiry)
            {
                return cachedToken;
            }

            var payload = new
            {
                grant_type = "client_credentials",
                client_id = config["InfinitePay:ClientId"],
                client_secret = config["InfinitePay:ClientSecret"],
                scope
            };

            var response = await http.PostAsJsonAsync(
                $"{config["InfinitePay:BaseUrl"]}/v2/oauth/token",
                payload,
                ct);

            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Resposta de token inválida da InfinitePay.");

            cachedToken = tokenResponse.AccessToken;
            tokenExpiry = DateTime.UtcNow.AddSeconds(Math.Max(0, tokenResponse.ExpiresIn - 60));

            return cachedToken;
        }
        finally
        {
            authLock.Release();
        }
    }
}
