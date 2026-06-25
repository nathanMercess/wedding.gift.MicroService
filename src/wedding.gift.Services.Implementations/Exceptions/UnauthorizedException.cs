namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class UnauthorizedException(string code) : AppException(code, 401)
{
}
