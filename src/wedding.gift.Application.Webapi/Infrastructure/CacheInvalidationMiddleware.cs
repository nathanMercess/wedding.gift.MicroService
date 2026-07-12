using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class CacheInvalidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApplicationCacheService cacheService)
    {
        bool completed = false;

        try
        {
            await next(context);
            completed = true;
        }
        finally
        {
            if (completed &&
                !HttpMethods.IsGet(context.Request.Method) &&
                context.Response.StatusCode is >= 200 and < 400)
            {
                cacheService.Invalidate();
            }
        }
    }
}
