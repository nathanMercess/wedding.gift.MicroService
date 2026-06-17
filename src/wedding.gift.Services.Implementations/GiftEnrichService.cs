using System.Globalization;
using System.Text.RegularExpressions;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public partial class GiftEnrichService(IHttpClientFactory httpClientFactory) : IGiftEnrichService
{
    private static readonly string[] PriceMetaNames =
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
            throw new BadRequestException("URL inválida. Informe uma URL completa (https://...).");

        var client = httpClientFactory.CreateClient("enrich");

        string html;
        try
        {
            html = await client.GetStringAsync(uri, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BadRequestException("Não foi possível acessar o link informado. Verifique a URL e tente novamente.");
        }

        return new GiftEnrichResponseDto
        {
            Name = ExtractMeta(html, "og:title") ?? ExtractTitle(html),
            Description = ExtractMeta(html, "og:description") ?? ExtractMeta(html, "description"),
            Price = ExtractPrice(html),
            ImageUrl = ExtractMeta(html, "og:image"),
        };
    }

    private static string? ExtractMeta(string html, string property)
    {
        // Tenta property= (Open Graph) e depois name= (meta padrão)
        var match = MetaPropertyRegex().Match(html);
        while (match.Success)
        {
            if (match.Groups["prop"].Value.Equals(property, StringComparison.OrdinalIgnoreCase))
                return DecodeHtml(match.Groups["content"].Value.Trim());

            match = match.NextMatch();
        }

        match = MetaNameRegex().Match(html);
        while (match.Success)
        {
            if (match.Groups["name"].Value.Equals(property, StringComparison.OrdinalIgnoreCase))
                return DecodeHtml(match.Groups["content"].Value.Trim());

            match = match.NextMatch();
        }

        return null;
    }

    private static decimal? ExtractPrice(string html)
    {
        foreach (var metaName in PriceMetaNames)
        {
            var raw = ExtractMeta(html, metaName);
            if (raw is null)
                continue;

            var digits = Regex.Replace(raw, @"[^\d,\.]", "");
            digits = digits.Replace(",", ".");

            if (decimal.TryParse(digits, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return Math.Round(value, 2);
        }

        return null;
    }

    private static string? ExtractTitle(string html)
    {
        var m = TitleTagRegex().Match(html);
        return m.Success ? DecodeHtml(m.Groups["title"].Value.Trim()) : null;
    }

    private static string DecodeHtml(string value) =>
        value
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&nbsp;", " ");

    [GeneratedRegex(@"<meta\s[^>]*property=[""'](?<prop>[^""']+)[""'][^>]*content=[""'](?<content>[^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaPropertyRegex();

    [GeneratedRegex(@"<meta\s[^>]*name=[""'](?<name>[^""']+)[""'][^>]*content=[""'](?<content>[^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MetaNameRegex();

    [GeneratedRegex(@"<title[^>]*>(?<title>[^<]*)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTagRegex();
}
