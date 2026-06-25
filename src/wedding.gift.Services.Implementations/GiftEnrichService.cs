using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public partial class GiftEnrichService(IHttpClientFactory httpClientFactory) : IGiftEnrichService
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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BadRequestException(ErrorCodes.INVALID_PRODUCT_URL);

        var client = httpClientFactory.CreateClient("enrich");

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
        var product = ExtractFromJsonLd(html);
        var metas = ParseMetaTags(html);

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
            var json = script.Groups["json"].Value.Trim();
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
                var product = FindProductNode(document.RootElement);
                if (product is null)
                    continue;

                var node = product.Value;
                var name = GetJsonString(node, "name");
                var description = GetJsonString(node, "description");
                var image = ExtractUrl(node, "image");
                var price = ExtractPriceFromOffers(node);

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
            foreach (var item in element.EnumerateArray())
            {
                var found = FindProductNode(item);
                if (found is not null)
                    return found;
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (IsProduct(element))
            return element;

        if (element.TryGetProperty("@graph", out var graph))
            return FindProductNode(graph);

        return null;
    }

    private static bool IsProduct(JsonElement obj)
    {
        if (!obj.TryGetProperty("@type", out var type))
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
        if (obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : DecodeHtml(text.Trim());
        }

        return null;
    }

    private static string? ExtractUrl(JsonElement obj, string property)
    {
        if (!obj.TryGetProperty(property, out var value))
            return null;

        return ReadUrl(value);
    }

    private static string? ReadUrl(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var url = element.GetString();
                return string.IsNullOrWhiteSpace(url) ? null : url.Trim();

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = ReadUrl(item);
                    if (found is not null)
                        return found;
                }

                return null;

            case JsonValueKind.Object:
                return element.TryGetProperty("url", out var nested) ? ReadUrl(nested) : null;

            default:
                return null;
        }
    }

    private static decimal? ExtractPriceFromOffers(JsonElement node)
    {
        if (!node.TryGetProperty("offers", out var offers))
            return null;

        return ReadOfferPrice(offers);
    }

    private static decimal? ReadOfferPrice(JsonElement offers)
    {
        if (offers.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in offers.EnumerateArray())
            {
                var found = ReadOfferPrice(item);
                if (found is not null)
                    return found;
            }

            return null;
        }

        if (offers.ValueKind != JsonValueKind.Object)
            return null;

        if (offers.TryGetProperty("price", out var price))
        {
            var value = ReadPriceValue(price);
            if (value is not null)
                return value;
        }

        if (offers.TryGetProperty("lowPrice", out var lowPrice))
        {
            var value = ReadPriceValue(lowPrice);
            if (value is not null)
                return value;
        }

        if (offers.TryGetProperty("priceSpecification", out var spec))
            return ReadOfferPrice(spec);

        return null;
    }

    private static decimal? ReadPriceValue(JsonElement price)
    {
        if (price.ValueKind == JsonValueKind.Number && price.TryGetDecimal(out var number))
            return Math.Round(number, 2);

        if (price.ValueKind == JsonValueKind.String && TryParsePrice(price.GetString()!, out var parsed))
            return parsed;

        return null;
    }

    private static List<(string Key, string Content)> ParseMetaTags(string html)
    {
        var result = new List<(string, string)>();

        foreach (Match tag in MetaTagRegex().Matches(html))
        {
            var attributes = ParseAttributes(tag.Value);

            if (!attributes.TryGetValue("content", out var content))
                continue;

            if (attributes.TryGetValue("property", out var property))
                result.Add((property, content));

            if (attributes.TryGetValue("name", out var name))
                result.Add((name, content));
        }

        return result;
    }

    private static Dictionary<string, string> ParseAttributes(string tag)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match attr in AttributeRegex().Matches(tag))
            attributes[attr.Groups["key"].Value] = attr.Groups["value"].Value;

        return attributes;
    }

    private static string? FindMeta(List<(string Key, string Content)> metas, string key)
    {
        foreach (var (metaKey, content) in metas)
        {
            if (metaKey.Equals(key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(content))
                return DecodeHtml(content.Trim());
        }

        return null;
    }

    private static decimal? ExtractPrice(List<(string Key, string Content)> metas)
    {
        foreach (var key in PriceMetaKeys)
        {
            var raw = FindMeta(metas, key);
            if (raw is null)
                continue;

            if (TryParsePrice(raw, out var value))
                return value;
        }

        return null;
    }

    public static bool TryParsePrice(string raw, out decimal value)
    {
        value = 0;

        var digits = Regex.Replace(raw, @"[^\d,\.]", "");
        if (string.IsNullOrEmpty(digits))
            return false;

        var hasComma = digits.Contains(',');
        var hasDot = digits.Contains('.');

        if (hasComma && hasDot)
        {
            // O último separador é o decimal; o outro é separador de milhar.
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

        if (decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            value = Math.Round(parsed, 2);
            return true;
        }

        return false;
    }

    private static string NormalizeSingleSeparator(string digits, char separator)
    {
        var occurrences = digits.Count(c => c == separator);
        var decimalsAfter = digits.Length - digits.LastIndexOf(separator) - 1;

        // Múltiplas ocorrências (1.234.567) ou não terminado em 2 casas = separador de milhar.
        if (occurrences > 1 || decimalsAfter != 2)
            return digits.Replace(separator.ToString(), "");

        // Único separador seguido de 2 dígitos = decimal.
        return digits.Replace(separator, '.');
    }

    private static string? ExtractTitle(string html)
    {
        var match = TitleTagRegex().Match(html);
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
