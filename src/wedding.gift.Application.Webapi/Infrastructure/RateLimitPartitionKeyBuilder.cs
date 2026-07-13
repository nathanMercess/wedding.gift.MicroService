namespace wedding.gift.Application.Webapi.Infrastructure;

public abstract class RateLimitPartitionKeyBuilder
{
    public static string PaymentPolling(HttpContext context)
    {
        string? resourceId = context.Request.RouteValues["orderId"]?.ToString()
                             ?? context.Request.RouteValues["mpOrderId"]?.ToString();

        if (!string.IsNullOrWhiteSpace(resourceId))
            return $"payment-polling:{resourceId.Trim().ToLowerInvariant()}";

        return $"payment-polling:{context.Request.Path.Value?.ToLowerInvariant()}:{ClientAddress(context)}";
    }

    public static string ClientAddress(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
