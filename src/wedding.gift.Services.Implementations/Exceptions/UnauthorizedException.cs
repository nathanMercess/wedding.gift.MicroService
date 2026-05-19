namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class UnauthorizedException(string detail) : AppException("Não autorizado", detail, 401)
{
}
