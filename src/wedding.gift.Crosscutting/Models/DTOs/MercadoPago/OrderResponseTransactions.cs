using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public class OrderResponseTransactions
{
    [JsonPropertyName("payments")]
    public List<OrderResponsePayment> Payments { get; set; } = new();
}
