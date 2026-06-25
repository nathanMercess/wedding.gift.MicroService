namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class BadRequestException(string code) : AppException(code, 400)
{
}
