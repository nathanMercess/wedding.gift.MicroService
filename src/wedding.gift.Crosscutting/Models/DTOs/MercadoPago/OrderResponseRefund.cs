using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderResponseRefund
{
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;
}
