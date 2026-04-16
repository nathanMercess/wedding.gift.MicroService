namespace wedding.gift.Services.Implementations.Exceptions;

public sealed class NotFoundException(string detail) : AppException("Recurso não encontrado", detail, 404)
{
}
