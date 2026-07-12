namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class TooManyRequestsException(string code) : AppException(code, 429);
