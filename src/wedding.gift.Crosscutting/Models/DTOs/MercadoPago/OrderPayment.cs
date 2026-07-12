using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderPayment
{
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("payment_method")]
    public OrderPaymentMethod PaymentMethod { get; set; } = new();

    [JsonPropertyName("expiration_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpirationTime { get; set; }
}
