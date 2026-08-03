using System.Globalization;
using System.Text;

namespace wedding.gift.Services.Implementations.Extensions;

public static class GuestNameNormalizer
{
    public static string Clean(string value)
        => string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static string Normalize(string value)
    {
        string decomposed = Clean(value).Normalize(NormalizationForm.FormD);
        StringBuilder normalized = new(decomposed.Length);

        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                normalized.Append(character);
        }

        return normalized.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }
}
