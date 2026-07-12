namespace wedding.gift.Crosscutting.Constants;

public static class GiftDisplayModes
{
    public const string Traditional = "Traditional";
    public const string PrivateUnlimited = "PrivateUnlimited";

    public static string Normalize(string? value)
    {
        if (string.Equals(value, PrivateUnlimited, StringComparison.OrdinalIgnoreCase))
            return PrivateUnlimited;

        return Traditional;
    }

    public static bool AllowsUnlimitedPurchases(string? value)
        => string.Equals(Normalize(value), PrivateUnlimited, StringComparison.Ordinal);
}
