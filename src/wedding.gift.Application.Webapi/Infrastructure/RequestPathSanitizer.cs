namespace wedding.gift.Application.Webapi.Infrastructure;

public static class RequestPathSanitizer
{
    private const string LookupPrefix = "/api/payment/order-lookup/";

    public static string Sanitize(PathString path)
    {
        string value = path.Value ?? string.Empty;
        if (value.StartsWith(LookupPrefix, StringComparison.OrdinalIgnoreCase) &&
            !value.Equals($"{LookupPrefix}request", StringComparison.OrdinalIgnoreCase))
        {
            return $"{LookupPrefix}[redacted]";
        }

        return value;
    }
}
