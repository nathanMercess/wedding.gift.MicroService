using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Notifications;

namespace wedding.gift.Services.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        return services;
    }
}
