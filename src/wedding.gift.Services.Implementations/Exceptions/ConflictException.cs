namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class ConflictException(string detail) : AppException("Conflito de negócio", detail, 409)
{
}
