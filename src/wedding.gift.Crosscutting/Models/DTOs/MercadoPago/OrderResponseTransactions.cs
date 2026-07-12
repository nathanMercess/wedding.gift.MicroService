using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderResponseTransactions
{
    [JsonPropertyName("payments")]
    public List<OrderResponsePayment> Payments { get; set; } = new();

    [JsonPropertyName("refunds")]
    public List<OrderResponseRefund> Refunds { get; set; } = new();
}
