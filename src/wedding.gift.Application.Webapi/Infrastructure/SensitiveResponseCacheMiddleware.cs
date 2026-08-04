namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class SensitiveResponseCacheMiddleware(RequestDelegate next)
{
    private static readonly PathString[] SensitivePrefixes =
    [
        new("/api/admin"),
        new("/api/auth"),
        new("/api/contributions"),
        new("/api/guest-confirmations"),
        new("/api/payment"),
        new("/api/webhook")
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (SensitivePrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
        {
            context.Response.Headers.CacheControl = "no-store, private";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }

        await next(context);
    }
}
