using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderPayer
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("identification")]
    public OrderPayerIdentification? Identification { get; set; }
}
