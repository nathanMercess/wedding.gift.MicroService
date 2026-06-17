using Xunit;
using wedding.gift.Services.Implementations;

namespace wedding.gift.Tests;

public class GiftEnrichServiceTests
{
    [Fact]
    public void ParseHtml_ExtractsOpenGraphTags_PropertyBeforeContent()
    {
        var html = """
            <html><head>
            <meta property="og:title" content="Cafeteira Expresso" />
            <meta property="og:description" content="Cafeteira automática para cápsulas." />
            <meta property="og:image" content="https://cdn.site.com/cafeteira.jpg" />
            <meta property="product:price:amount" content="699.90" />
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Cafeteira Expresso", result.Name);
        Assert.Equal("Cafeteira automática para cápsulas.", result.Description);
        Assert.Equal("https://cdn.site.com/cafeteira.jpg", result.ImageUrl);
        Assert.Equal(699.90m, result.Price);
    }

    [Fact]
    public void ParseHtml_ExtractsOpenGraphTags_ContentBeforeProperty()
    {
        // Muitos sites reais colocam content antes de property.
        var html = """
            <html><head>
            <meta content="Jogo de Panelas Inox" property="og:title">
            <meta content="Conjunto com 5 panelas." property="og:description">
            <meta content="https://cdn.site.com/panelas.jpg" property="og:image">
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Jogo de Panelas Inox", result.Name);
        Assert.Equal("Conjunto com 5 panelas.", result.Description);
        Assert.Equal("https://cdn.site.com/panelas.jpg", result.ImageUrl);
    }

    [Fact]
    public void ParseHtml_FallsBackToTitleTag_WhenNoOgTitle()
    {
        var html = "<html><head><title>Produto Sem OG</title></head></html>";

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Produto Sem OG", result.Name);
    }

    [Fact]
    public void ParseHtml_UsesNameAttribute_ForDescriptionFallback()
    {
        var html = """
            <html><head>
            <title>Produto</title>
            <meta name="description" content="Descrição via meta name.">
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Descrição via meta name.", result.Description);
    }

    [Fact]
    public void ParseHtml_DecodesHtmlEntities()
    {
        var html = """<meta property="og:title" content="Talheres &amp; Copos &quot;Premium&quot;">""";

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Talheres & Copos \"Premium\"", result.Name);
    }

    [Fact]
    public void ParseHtml_PrefersTwitterTitle_WhenNoOgTitle()
    {
        var html = """
            <html><head>
            <title>Fallback Title</title>
            <meta name="twitter:title" content="Título do Twitter">
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Título do Twitter", result.Name);
    }

    [Fact]
    public void ParseHtml_ReturnsNulls_WhenNoMetadata()
    {
        var html = "<html><head></head><body>Sem nada útil</body></html>";

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Null(result.Name);
        Assert.Null(result.Description);
        Assert.Null(result.ImageUrl);
        Assert.Null(result.Price);
    }

    [Theory]
    [InlineData("R$ 1.299,90", 1299.90)]
    [InlineData("1299.90", 1299.90)]
    [InlineData("699,90", 699.90)]
    [InlineData("R$ 2.000", 2000)]
    [InlineData("1.234.567,89", 1234567.89)]
    [InlineData("50", 50)]
    public void TryParsePrice_HandlesVariousFormats(string raw, decimal expected)
    {
        var ok = GiftEnrichService.TryParsePrice(raw, out var value);

        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("grátis")]
    [InlineData("sob consulta")]
    public void TryParsePrice_ReturnsFalse_WhenNoDigits(string raw)
    {
        var ok = GiftEnrichService.TryParsePrice(raw, out _);

        Assert.False(ok);
    }

    [Fact]
    public void ParseHtml_ExtractsBrazilianPriceFromMeta()
    {
        var html = """<meta property="product:price:amount" content="R$ 1.299,90">""";

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal(1299.90m, result.Price);
    }

    [Fact]
    public void ParseHtml_ExtractsFromJsonLdProduct()
    {
        var html = """
            <html><head>
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@type": "Product",
              "name": "Kit 84 Figurinhas Copa 2026",
              "description": "Pacote com 84 figurinhas do álbum.",
              "image": "https://cdn.ml.com/figurinhas.jpg",
              "offers": { "@type": "Offer", "price": "129.90", "priceCurrency": "BRL" }
            }
            </script>
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Kit 84 Figurinhas Copa 2026", result.Name);
        Assert.Equal("Pacote com 84 figurinhas do álbum.", result.Description);
        Assert.Equal("https://cdn.ml.com/figurinhas.jpg", result.ImageUrl);
        Assert.Equal(129.90m, result.Price);
    }

    [Fact]
    public void ParseHtml_JsonLd_HandlesImageArray()
    {
        var html = """
            <script type="application/ld+json">
            { "@type": "Product", "name": "Produto", "image": ["https://a.com/1.jpg", "https://a.com/2.jpg"] }
            </script>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("https://a.com/1.jpg", result.ImageUrl);
    }

    [Fact]
    public void ParseHtml_JsonLd_HandlesGraphAndOffersArray()
    {
        var html = """
            <script type="application/ld+json">
            {
              "@context": "https://schema.org",
              "@graph": [
                { "@type": "WebPage", "name": "Página" },
                { "@type": "Product", "name": "Aspirador Robô", "offers": [ { "@type": "Offer", "price": 1299.00 } ] }
              ]
            }
            </script>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Aspirador Robô", result.Name);
        Assert.Equal(1299.00m, result.Price);
    }

    [Fact]
    public void ParseHtml_JsonLd_TakesPrecedenceOverOgTags()
    {
        var html = """
            <html><head>
            <meta property="og:title" content="Título OG Genérico">
            <script type="application/ld+json">
            { "@type": "Product", "name": "Nome Real do Produto", "offers": { "price": "49.90" } }
            </script>
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Nome Real do Produto", result.Name);
        Assert.Equal(49.90m, result.Price);
    }

    [Fact]
    public void ParseHtml_JsonLd_IgnoresInvalidJsonAndFallsBackToOg()
    {
        var html = """
            <html><head>
            <meta property="og:title" content="Título OG">
            <script type="application/ld+json">{ isto não é json válido }</script>
            </head></html>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal("Título OG", result.Name);
    }

    [Fact]
    public void ParseHtml_JsonLd_ReadsPriceFromPriceSpecification()
    {
        var html = """
            <script type="application/ld+json">
            { "@type": "Product", "name": "Produto",
              "offers": { "@type": "Offer", "priceSpecification": { "price": "899.90", "priceCurrency": "BRL" } } }
            </script>
            """;

        var result = GiftEnrichService.ParseHtml(html);

        Assert.Equal(899.90m, result.Price);
    }
}
