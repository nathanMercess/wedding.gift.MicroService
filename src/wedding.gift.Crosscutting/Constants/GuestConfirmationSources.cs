namespace wedding.gift.Crosscutting.Constants;

public static class GuestConfirmationSources
{
    public const string RegisteredList = "RegisteredList";
    public const string FreeText = "FreeText";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        RegisteredList,
        FreeText
    };
}
