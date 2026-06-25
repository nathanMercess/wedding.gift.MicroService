namespace wedding.gift.Services.Implementations.Exceptions;

public abstract class AppException(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
