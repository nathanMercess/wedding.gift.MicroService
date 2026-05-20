namespace wedding.gift.Application.Webapi.Services.Interfaces;

public interface IInfinitePayAuthService
{
    Task<string> GetAccessTokenAsync(string scope, CancellationToken ct = default);
}
