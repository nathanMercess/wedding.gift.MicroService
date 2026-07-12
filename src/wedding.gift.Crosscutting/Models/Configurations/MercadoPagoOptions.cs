using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.Configurations;

public sealed class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.mercadopago.com";

    [Required]
    [MinLength(32)]
    public string WebhookSecret { get; set; } = string.Empty;
}
