using Microsoft.AspNetCore.Http;

namespace wedding.gift.Application.Webapi.Services.Exceptions;

public sealed class NotFoundException(string detail) : AppException("Recurso não encontrado", detail, StatusCodes.Status404NotFound)
{
}
