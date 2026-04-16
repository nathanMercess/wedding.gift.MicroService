using InfluencyMe.Framework.Repository.EFCore;
using InfluencyMe.Framework.Repository.Pattern.DataContext;
using InfluencyMe.Framework.Repository.Pattern.UnitOfWork;
using wedding-gift-api.Infra.Implementations.DataContext;
using Microsoft.Extensions.DependencyInjection;

namespace wedding-gift-api.Infra.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddMicroServiceContext(this IServiceCollection services)
    {
        services.AddScoped<IEfCoreDataContext, MicroServiceContext>();

        return services;
    }

    public static IServiceCollection AddUnitOfWork(this IServiceCollection services)
    {
        services.AddScoped<IEfCoreUnitOfWork, EfCoreUnitOfWork>();

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services;
    }
}
