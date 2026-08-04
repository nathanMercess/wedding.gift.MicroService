using Microsoft.AspNetCore.Http;
using wedding.gift.Application.Webapi.Infrastructure;
using Xunit;

namespace wedding.gift.Tests;

public sealed class SensitiveResponseCacheMiddlewareTests
{
    [Theory]
    [InlineData("/api/payment/order/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/auth/profile")]
    [InlineData("/api/admin/payments")]
    [InlineData("/api/guest-confirmations")]
    public async Task InvokeAsync_DeveImpedirArmazenamentoDeRespostaSensivel(string path)
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        SensitiveResponseCacheMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("no-store, private", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }

    [Fact]
    public async Task InvokeAsync_NaoDeveForcarNoStoreEmListaPublica()
    {
        DefaultHttpContext context = new();
        context.Request.Path = "/api/gifts";
        SensitiveResponseCacheMiddleware middleware = new(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }
}
