namespace wedding.gift.Crosscutting.Constants;

public static class PaymentErrorCodes
{
    public const string PaymentDeclined = "PAYMENT_DECLINED";
    public const string InsufficientAmount = "INSUFFICIENT_AMOUNT";
    public const string DuplicateOrder = "DUPLICATE_ORDER";
    public const string PixExpired = "PIX_EXPIRED";
    public const string PixRejected = "PIX_REJECTED";
    public const string InvalidCardToken = "INVALID_CARD_TOKEN";
    public const string ProviderError = "PROVIDER_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string OrderNotFound = "PAYMENT_ORDER_NOT_FOUND";
}
