using Microsoft.AspNetCore.Http;
using System.Net;
using wedding.gift.Application.Webapi.Infrastructure;
using Xunit;

namespace wedding.gift.Tests;

public sealed class RateLimitPartitionKeyBuilderTests
{
    [Fact]
    public void PaymentPolling_DeveParticionarPorPedidoMesmoQuandoIpEhCompartilhado()
    {
        DefaultHttpContext first = CreateContext("7a1c60f8-e25f-4d4a-9151-c8909b98bf68");
        DefaultHttpContext second = CreateContext("94b558ff-cdd6-4673-ab22-215381d40114");

        string firstKey = RateLimitPartitionKeyBuilder.PaymentPolling(first);
        string secondKey = RateLimitPartitionKeyBuilder.PaymentPolling(second);

        Assert.NotEqual(firstKey, secondKey);
        Assert.Contains("7a1c60f8-e25f-4d4a-9151-c8909b98bf68", firstKey);
        Assert.Contains("94b558ff-cdd6-4673-ab22-215381d40114", secondKey);
    }

    [Fact]
    public void PaymentPolling_DeveCompartilharParticaoParaMesmoPedidoEmIpsDiferentes()
    {
        DefaultHttpContext first = CreateContext("7a1c60f8-e25f-4d4a-9151-c8909b98bf68");
        DefaultHttpContext second = CreateContext("7a1c60f8-e25f-4d4a-9151-c8909b98bf68");
        second.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.20");

        Assert.Equal(
            RateLimitPartitionKeyBuilder.PaymentPolling(first),
            RateLimitPartitionKeyBuilder.PaymentPolling(second));
    }

    private static DefaultHttpContext CreateContext(string orderId)
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
        context.Request.RouteValues["orderId"] = orderId;
        return context;
    }
}
