using Google.Cloud.Storage.V1;
using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICoupleService, CoupleService>();
        services.AddTransient<IEmailService, EmailService>();

        services.AddSingleton(_ => StorageClient.Create());
        services.AddScoped<IImageUploadService, ImageUploadService>();

        return services;
    }
}
