namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class NotFoundException(string code) : AppException(code, 404)
{
}
