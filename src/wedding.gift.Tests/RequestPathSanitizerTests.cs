using Microsoft.AspNetCore.Http;
using wedding.gift.Application.Webapi.Infrastructure;
using Xunit;

namespace wedding.gift.Tests;

public sealed class RequestPathSanitizerTests
{
    [Fact]
    public void Sanitize_DeveOcultarOrderIdDePagamento()
    {
        string sanitized = RequestPathSanitizer.Sanitize(new PathString("/api/payment/order/6ba7b810-9dad-11d1-80b4-00c04fd430c8"));

        Assert.Equal("/api/payment/order/[redacted]", sanitized);
    }
}
