namespace wedding.gift.Crosscutting.Constants;

public static class GiftCategories
{
    public const string Cozinha = "Cozinha";
    public const string Eletrodomesticos = "Eletrodomésticos";
    public const string Quarto = "Quarto";
    public const string Mesa = "Mesa";
    public const string Casa = "Casa";

    public static readonly IReadOnlyList<string> All = [Cozinha, Eletrodomesticos, Quarto, Mesa, Casa];

    public static bool IsValid(string? category)
        => !string.IsNullOrWhiteSpace(category) && All.Contains(category.Trim(), StringComparer.Ordinal);
}
