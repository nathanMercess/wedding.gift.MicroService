using System.Text.Json.Serialization;

namespace wedding.gift.Application.Webapi.Models.InfinitePay;

public class TransacaoApiResponse
{
    [JsonPropertyName("data")]
    public TransacaoData? Data { get; set; }
}

public class TransacaoData
{
    [JsonPropertyName("attributes")]
    public TransacaoAttributes? Attributes { get; set; }
}

public class TransacaoAttributes
{
    [JsonPropertyName("nsu")]
    public string? Nsu { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("br_code")]
    public string? BrCode { get; set; }
}
