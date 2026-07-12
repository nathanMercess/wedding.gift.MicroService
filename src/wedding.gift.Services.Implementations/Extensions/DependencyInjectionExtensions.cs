using Microsoft.Extensions.DependencyInjection;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Email;

namespace wedding.gift.Services.Implementations.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationCacheService, ApplicationCacheService>();
        services.AddScoped<IApiRequestLogService, ApiRequestLogService>();
        services.AddScoped<IGiftService, GiftService>();
        services.AddScoped<IContributionService, ContributionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICoupleService, CoupleService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IOrderLookupService, OrderLookupService>();
        services.AddScoped<ICoupleOverviewService, CoupleOverviewService>();
        services.AddTransient<IEmailService, EmailService>();

        services.AddScoped<IImageUploadService, ImageUploadService>();
        services.AddScoped<IGiftEnrichService, GiftEnrichService>();

        services.AddHttpClient("enrich", c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; WeddingGiftBot/1.0)");
                c.Timeout = TimeSpan.FromSeconds(15);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        return services;
    }
}
