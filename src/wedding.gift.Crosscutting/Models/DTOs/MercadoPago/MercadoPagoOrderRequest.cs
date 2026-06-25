using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class MercadoPagoOrderRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "online";

    [JsonPropertyName("processing_mode")]
    public string ProcessingMode { get; set; } = "automatic";

    [JsonPropertyName("total_amount")]
    public string TotalAmount { get; set; } = string.Empty;

    [JsonPropertyName("external_reference")]
    public string ExternalReference { get; set; } = string.Empty;

    [JsonPropertyName("payer")]
    public OrderPayer Payer { get; set; } = new();

    [JsonPropertyName("transactions")]
    public OrderTransactions Transactions { get; set; } = new();
}
