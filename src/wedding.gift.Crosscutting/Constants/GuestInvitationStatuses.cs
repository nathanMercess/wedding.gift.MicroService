namespace wedding.gift.Crosscutting.Constants;

public static class GuestInvitationStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Confirmed = "Confirmed";
    public const string Pending = "Pending";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Active,
        Inactive,
        Confirmed,
        Pending
    };
}
