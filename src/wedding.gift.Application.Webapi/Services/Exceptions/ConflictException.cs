using Microsoft.AspNetCore.Http;

namespace wedding.gift.Application.Webapi.Services.Exceptions;

public sealed class ConflictException(string detail) : AppException("Conflito de negócio", detail, StatusCodes.Status409Conflict)
{
}
