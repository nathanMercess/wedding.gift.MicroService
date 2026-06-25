using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class MercadoPagoOrderResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; set; }

    [JsonPropertyName("transactions")]
    public OrderResponseTransactions? Transactions { get; set; }
}
