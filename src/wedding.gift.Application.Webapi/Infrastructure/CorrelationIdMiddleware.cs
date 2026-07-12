using System.Text.RegularExpressions;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        string incoming = context.Request.Headers[HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(incoming) && CorrelationIdRegex().IsMatch(incoming))
            context.TraceIdentifier = incoming;

        context.Response.Headers[HeaderName] = context.TraceIdentifier;
        await next(context);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{8,100}$")]
    private static partial Regex CorrelationIdRegex();
}
