using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed partial class GiftEnrichService(IHttpClientFactory httpClientFactory) : IGiftEnrichService
{
    private static readonly string[] PriceMetaKeys =
    [
        "product:price:amount",
        "og:price:amount",
        "twitter:data1",
        "price",
    ];

    public async Task<GiftEnrichResponseDto> EnrichAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException(ErrorCodes.INVALID_PRODUCT_URL);

        HttpClient client = httpClientFactory.CreateClient("enrich");

        string html;
        try
        {
            html = await client.GetStringAsync(uri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BadRequestException(ErrorCodes.PRODUCT_URL_UNREACHABLE);
        }

        return ParseHtml(html);
    }

    public static GiftEnrichResponseDto ParseHtml(string html)
    {
        (string Name, string Description, decimal? Price, string ImageUrl) product = ExtractFromJsonLd(html);
        List<(string Key, string Content)> metas = ParseMetaTags(html);

        return new GiftEnrichResponseDto
        {
            Name = product.Name ?? FindMeta(metas, "og:title") ?? FindMeta(metas, "twitter:title") ?? ExtractTitle(html),
            Description = product.Description ?? FindMeta(metas, "og:description") ?? FindMeta(metas, "twitter:description") ?? FindMeta(metas, "description"),
            Price = product.Price ?? ExtractPrice(metas),
            ImageUrl = product.ImageUrl ?? FindMeta(metas, "og:image") ?? FindMeta(metas, "twitter:image"),
        };
    }

    private static (string? Name, string? Description, decimal? Price, string? ImageUrl) ExtractFromJsonLd(string html)
    {
        foreach (Match script in JsonLdScriptRegex().Matches(html))
        {
            string json = script.Groups["json"].Value.Trim();
            if (string.IsNullOrEmpty(json))
                continue;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                continue;
            }

            using (document)
            {
                JsonElement? product = FindProductNode(document.RootElement);
                if (product is null)
                    continue;

                JsonElement node = product.Value;
                string name = GetJsonString(node, "name");
                string description = GetJsonString(node, "description");
                string image = ExtractUrl(node, "image");
                decimal? price = ExtractPriceFromOffers(node);

                if (name is not null || price is not null || image is not null)
                    return (name, description, price, image);
            }
        }

        return (null, null, null, null);
    }

    private static JsonElement? FindProductNode(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                JsonElement? found = FindProductNode(item);
                if (found is not null)
                    return found;
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (IsProduct(element))
            return element;

        if (element.TryGetProperty("@graph", out JsonElement graph))
            return FindProductNode(graph);

        return null;
    }

    private static bool IsProduct(JsonElement obj)
    {
        if (!obj.TryGetProperty("@type", out JsonElement type))
            return false;

        if (type.ValueKind == JsonValueKind.String)
            return type.GetString()?.Contains("Product", StringComparison.OrdinalIgnoreCase) == true;

        if (type.ValueKind == JsonValueKind.Array)
            return type.EnumerateArray().Any(t =>
                t.ValueKind == JsonValueKind.String &&
                t.GetString()?.Contains("Product", StringComparison.OrdinalIgnoreCase) == true);

        return false;
    }

    private static string? GetJsonString(JsonElement obj, string property)
    {
        if (obj.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : DecodeHtml(text.Trim());
        }

        return null;
    }

    private static string? ExtractUrl(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out JsonElement value))
            return null;

        return ReadUrl(value);
    }

    private static string? ReadUrl(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                string url = element.GetString();
                return string.IsNullOrWhiteSpace(url) ? null : url.Trim();

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    string found = ReadUrl(item);
                    if (found is not null)
                        return found;
                }

                return null;

            case JsonValueKind.Object:
                return element.TryGetProperty("url", out JsonElement nested) ? ReadUrl(nested) : null;

            default:
                return null;
        }
    }

    private static decimal? ExtractPriceFromOffers(JsonElement node)
    {
        if (!node.TryGetProperty("offers", out JsonElement offers))
            return null;

        return ReadOfferPrice(offers);
    }

    private static decimal? ReadOfferPrice(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in offers.EnumerateArray())
            {
                decimal? found = ReadOfferPrice(item);
                if (found is not null)
                    return found;
            }

            return null;
        }

        if (offers.ValueKind != JsonValueKind.Object)
            return null;

        if (offers.TryGetProperty("price", out JsonElement price))
        {
            decimal? value = ReadPriceValue(price);
            if (value is not null)
                return value;
        }

        if (offers.TryGetProperty("lowPrice", out JsonElement lowPrice))
        {
            decimal? value = ReadPriceValue(lowPrice);
            if (value is not null)
                return value;
        }

        if (offers.TryGetProperty("priceSpecification", out JsonElement spec))
            return ReadOfferPrice(spec);

        return null;
    }

    private static decimal? ReadPriceValue(JsonElement price)
    {
        if (price.ValueKind == JsonValueKind.Number && price.TryGetDecimal(out decimal number))
            return Math.Round(number, 2);

        if (price.ValueKind == JsonValueKind.String && TryParsePrice(price.GetString()!, out decimal parsed))
            return parsed;

        return null;
    }

    private static List<(string Key, string Content)> ParseMetaTags(string html)
    {
        List<(string Key, string Content)> result = [];

        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            Dictionary<string, string> attributes = ParseAttributes(tag.Value);

            if (!attributes.TryGetValue("content", out string content))
                continue;

            if (attributes.TryGetValue("property", out string property))
                result.Add((property, content));

            if (attributes.TryGetValue("name", out string name))
                result.Add((name, content));
        }

        return result;
    }

    private static Dictionary<string, string> ParseAttributes(string tag)
    {
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match attr in AttributeRegex().Matches(tag))
            attributes[attr.Groups["key"].Value] = attr.Groups["value"].Value;

        return attributes;
    }

    private static string? FindMeta(List<(string Key, string Content)> metas, string key)
    {
        foreach ((string metaKey, string content) in metas)
        {
            if (metaKey.Equals(key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(content))
                return DecodeHtml(content.Trim());
        }

        return null;
    }

    private static decimal? ExtractPrice(List<(string Key, string Content)> metas)
    {
        foreach (string key in PriceMetaKeys)
        {
            string raw = FindMeta(metas, key);
            if (raw is null)
                continue;

            if (TryParsePrice(raw, out decimal value))
                return value;
        }

        return null;
    }

    public static bool TryParsePrice(string raw, out decimal value)
    {
        value = 0;

        string digits = Regex.Replace(raw, @"[^\d,\.]", "");
        if (string.IsNullOrEmpty(digits))
            return false;

        bool hasComma = digits.Contains(',');
        bool hasDot = digits.Contains('.');

        if (hasComma && hasDot)
        {
            if (digits.LastIndexOf(',') > digits.LastIndexOf('.'))
                digits = digits.Replace(".", "").Replace(',', '.');
            else
                digits = digits.Replace(",", "");
        }
        else if (hasComma)
        {
            digits = NormalizeSingleSeparator(digits, ',');
        }
        else if (hasDot)
        {
            digits = NormalizeSingleSeparator(digits, '.');
        }

        if (decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
        {
            value = Math.Round(parsed, 2);
            return true;
        }

        return false;
    }

    private static string NormalizeSingleSeparator(string digits, char separator)
    {
        int occurrences = digits.Count(c => c == separator);
        int decimalsAfter = digits.Length - digits.LastIndexOf(separator) - 1;

        if (occurrences > 1 || decimalsAfter != 2)
            return digits.Replace(separator.ToString(), "");

        return digits.Replace(separator, '.');
    }

    private static string? ExtractTitle(string html)
    {
        Match match = TitleTagRegex().Match(html);
        return match.Success && !string.IsNullOrWhiteSpace(match.Groups["title"].Value)
            ? DecodeHtml(match.Groups["title"].Value.Trim())
            : null;
    }

    private static string DecodeHtml(string value) =>
        value
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&#x27;", "'")
            .Replace("&nbsp;", " ");

    [GeneratedRegex(@"<script[^>]*type=[""']application/ld\+json[""'][^>]*>(?<json>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLdScriptRegex();

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaTagRegex();

    [GeneratedRegex(@"(?<key>[\w:-]+)\s*=\s*[""'](?<value>[^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"<title[^>]*>(?<title>[^<]*)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTagRegex();
}
