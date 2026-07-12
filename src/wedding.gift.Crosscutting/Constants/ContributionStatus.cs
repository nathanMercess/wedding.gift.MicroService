namespace wedding.gift.Crosscutting.Constants;

public static class ContributionStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
    public const string Refunded = "Refunded";
    public const string Chargeback = "Chargeback";

    public static readonly HashSet<string> Allowed =
    [
        Pending,
        Paid,
        Cancelled,
        Refunded,
        Chargeback
    ];
}
