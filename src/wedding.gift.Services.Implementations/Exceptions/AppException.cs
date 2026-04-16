namespace wedding.gift.Services.Implementations.Exceptions;

public abstract class AppException(string title, string detail, int statusCode) : Exception(detail)
{
    public string Title { get; } = title;
    public int StatusCode { get; } = statusCode;
}
