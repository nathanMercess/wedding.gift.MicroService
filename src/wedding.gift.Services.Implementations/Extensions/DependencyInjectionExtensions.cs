using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
