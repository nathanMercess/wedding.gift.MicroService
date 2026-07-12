namespace wedding.gift.Crosscutting.Constants;

public static class PaymentStatuses
{
    public const string Approved = "approved";
    public const string Processed = "processed";
    public const string Created = "created";
    public const string Processing = "processing";
    public const string ActionRequired = "action_required";
    public const string Pending = "pending";
    public const string InProcess = "in_process";
    public const string InMediation = "in_mediation";
    public const string Rejected = "rejected";
    public const string Expired = "expired";
    public const string Cancelled = "cancelled";
    public const string Canceled = "canceled";
    public const string Failed = "failed";
    public const string Refunded = "refunded";
    public const string PartiallyRefunded = "partially_refunded";
    public const string ChargedBack = "charged_back";
    public const string Error = "error";

    public static readonly string[] Reserving = [Created, Processing, ActionRequired, Pending, InProcess, InMediation];
    public static readonly string[] Settled = [Approved, Processed];
    public static readonly string[] Released =
        [Rejected, Expired, Cancelled, Canceled, Failed, Refunded, PartiallyRefunded, ChargedBack, Error];

    public static bool IsReserving(string? status)
        => Reserving.Contains(status ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static bool IsSettled(string? status)
        => Settled.Contains(status ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static bool IsReversed(string? status)
        => status is not null &&
           new[] { Refunded, ChargedBack }.Contains(status, StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string? status, string? statusDetail = null)
    {
        if (string.IsNullOrWhiteSpace(status))
            return Error;

        string value = status.Trim().ToLowerInvariant();

        if (value == Processed &&
            string.Equals(statusDetail?.Trim(), PartiallyRefunded, StringComparison.OrdinalIgnoreCase))
        {
            return PartiallyRefunded;
        }

        if (value == Processed &&
            string.Equals(statusDetail?.Trim(), Refunded, StringComparison.OrdinalIgnoreCase))
        {
            return Refunded;
        }

        if (IsSettled(value))
            return Settled.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        if (IsReserving(value))
            return Reserving.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        if (Released.Contains(value, StringComparer.OrdinalIgnoreCase))
            return Released.First(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));

        return value;
    }
}
