using Microsoft.AspNetCore.Http;

namespace wedding.gift.Application.Webapi.Services.Exceptions;

public sealed class BadRequestException(string detail) : AppException("Requisição inválida", detail, StatusCodes.Status400BadRequest)
{
}
