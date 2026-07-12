using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Application.Webapi.Infrastructure;

public static class ApiBehaviorOptionsExtensions
{
    public static void UseValidationApiResponse(this ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            IReadOnlyDictionary<string, string[]> fields = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value!.Errors.Select(_ => ErrorCodes.FIELD_INVALID).ToArray());

            ApiResponseDto<object> response = ApiResponseDto<object>.Fail(
                ErrorCodes.VALIDATION_ERROR,
                context.HttpContext.TraceIdentifier,
                fields);

            return new BadRequestObjectResult(response);
        };
    }
}
