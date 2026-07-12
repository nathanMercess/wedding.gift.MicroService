namespace wedding.gift.Crosscutting.Constants;

public static class GiftCategories
{
    public static readonly IReadOnlyList<string> All =
    [
        "Cozinha",
        "Eletrodomésticos",
        "Quarto",
        "Mesa",
        "Casa"
    ];

    public static bool IsValid(string? category)
        => !string.IsNullOrWhiteSpace(category) && All.Contains(category.Trim(), StringComparer.Ordinal);
}
