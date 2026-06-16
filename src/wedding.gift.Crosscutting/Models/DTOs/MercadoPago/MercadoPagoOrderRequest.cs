using System.Text.Json.Serialization;

namespace wedding.gift.Crosscutting.Models.DTOs.MercadoPago;

public class MercadoPagoOrderRequest
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

public class OrderPayer
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("identification")]
    public OrderPayerIdentification? Identification { get; set; }
}

public class OrderPayerIdentification
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "CPF";

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;
}

public class OrderTransactions
{
    [JsonPropertyName("payments")]
    public List<OrderPayment> Payments { get; set; } = new();
}

public class OrderPayment
{
    [JsonPropertyName("amount")]
    public string Amount { get; set; } = string.Empty;

    [JsonPropertyName("payment_method")]
    public OrderPaymentMethod PaymentMethod { get; set; } = new();
}

public class OrderPaymentMethod
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Token { get; set; }

    [JsonPropertyName("installments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Installments { get; set; }

    [JsonPropertyName("issuer_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IssuerId { get; set; }
}
