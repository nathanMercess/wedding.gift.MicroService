using Google.Cloud.Storage.V1;
using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IApiRequestLogService, ApiRequestLogService>();
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICoupleService, CoupleService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddTransient<IEmailService, EmailService>();

        services.AddSingleton(_ => StorageClient.Create());
        services.AddScoped<IImageUploadService, ImageUploadService>();
        services.AddScoped<IGiftEnrichService, GiftEnrichService>();

        services.AddHttpClient("enrich", c =>
        {
            c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; WeddingGiftBot/1.0)");
            c.Timeout = TimeSpan.FromSeconds(15);
        });

        return services;
    }
}
