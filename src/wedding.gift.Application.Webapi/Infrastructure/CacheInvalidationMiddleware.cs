using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class CacheInvalidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApplicationCacheService cacheService)
    {
        try
        {
            await next(context);
        }
        finally
        {
            if (!HttpMethods.IsGet(context.Request.Method))
                cacheService.Invalidate();
        }
    }
}
