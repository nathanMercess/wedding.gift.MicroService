namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class ConflictException(string code) : AppException(code, 409)
{
}
