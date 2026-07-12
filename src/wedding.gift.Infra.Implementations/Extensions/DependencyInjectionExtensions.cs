using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.Repositories;

namespace wedding.gift.Infra.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IGiftRepository, GiftRepository>();
        services.AddScoped<IContributionRepository, ContributionRepository>();
        services.AddScoped<ICoupleRepository, CoupleRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IApiRequestLogRepository, ApiRequestLogRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOperationalRepository, OperationalRepository>();

        return services;
    }
}

