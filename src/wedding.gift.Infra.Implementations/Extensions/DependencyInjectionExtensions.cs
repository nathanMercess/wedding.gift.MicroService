using Microsoft.Extensions.DependencyInjection;

namespace wedding.gift.Infra.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMicroServiceContext(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services;
    }
}

