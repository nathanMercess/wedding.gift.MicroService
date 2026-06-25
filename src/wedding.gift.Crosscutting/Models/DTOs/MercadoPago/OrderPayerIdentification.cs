using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderPayerIdentification
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "CPF";

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;
}
