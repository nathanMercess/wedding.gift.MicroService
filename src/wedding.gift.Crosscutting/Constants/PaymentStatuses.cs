namespace wedding.gift.Crosscutting.Constants;

public static class PaymentStatuses
{
    public const string Approved = "approved";
    public const string Processed = "processed";
    public const string Pending = "pending";
    public const string InProcess = "in_process";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
    public const string Error = "error";

    public static readonly string[] Reserving = [Pending, InProcess];
    public static readonly string[] Settled = [Approved, Processed];
    public static readonly string[] Released = [Rejected, Expired, Cancelled, Error];

    public static bool IsReserving(string? status)
        => Reserving.Contains(status ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static bool IsSettled(string? status)
        => Settled.Contains(status ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return Error;

        string value = status.Trim();

        if (IsSettled(value))
            return Settled.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        if (IsReserving(value))
            return Reserving.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        if (Released.Contains(value, StringComparer.OrdinalIgnoreCase))
            return Released.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        return Error;
    }
}
