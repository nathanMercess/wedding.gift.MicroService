using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderTransactions
{
    [JsonPropertyName("payments")]
    public List<OrderPayment> Payments { get; set; } = new();
}
