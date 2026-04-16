namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class BadRequestException(string detail) : AppException("Requisição inválida", detail, 400)
{
}
