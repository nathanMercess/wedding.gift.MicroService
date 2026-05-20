namespace wedding.gift.Application.Webapi.Models.InfinitePay;

public class TransacaoPayload
{
    public int Amount { get; set; }
    public string CaptureMethod { get; set; } = string.Empty;
    public int Installments { get; set; } = 1;
    public TransacaoPayment? Payment { get; set; }
    public TransacaoMetadata Metadata { get; set; } = new();
}

public class TransacaoPayment
{
    public string Type { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class TransacaoMetadata
{
    public string OrderId { get; set; } = string.Empty;
    public TransacaoCallback Callback { get; set; } = new();
}

public class TransacaoCallback
{
    public string? Validate { get; set; }
    public string? Confirm { get; set; }
    public string Secret { get; set; } = string.Empty;
}
