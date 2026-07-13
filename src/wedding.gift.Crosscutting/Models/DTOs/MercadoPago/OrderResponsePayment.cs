using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public sealed class OrderResponsePayment
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("status_detail")]
    public string? StatusDetail { get; set; }

    [JsonPropertyName("amount")]
    public string? Amount { get; set; }

    [JsonPropertyName("refunded_amount")]
    public string? RefundedAmount { get; set; }

    [JsonPropertyName("payment_method")]
    public OrderResponsePaymentMethod? PaymentMethod { get; set; }
}
