namespace wedding.gift.Crosscutting.Constants;

public static class ContributionStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";

    public static readonly HashSet<string> Allowed =
    [
        Pending,
        Paid,
        Cancelled
    ];
}
