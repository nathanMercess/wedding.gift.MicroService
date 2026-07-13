namespace wedding.gift.Application.Webapi.Infrastructure;

public static class RequestPathSanitizer
{
    private const string LookupPrefix = "/api/payment/order-lookup/";
    private const string PaymentOrderPrefix = "/api/payment/order/";

    public static string Sanitize(PathString path)
    {
        string value = path.Value ?? string.Empty;
        if (value.StartsWith(LookupPrefix, StringComparison.OrdinalIgnoreCase) &&
            !value.Equals($"{LookupPrefix}request", StringComparison.OrdinalIgnoreCase))
        {
            return $"{LookupPrefix}[redacted]";
        }

        if (value.StartsWith(PaymentOrderPrefix, StringComparison.OrdinalIgnoreCase))
            return $"{PaymentOrderPrefix}[redacted]";

        return value;
    }
}
