using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;

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

            _ = QueueValidationNotificationAsync(
                context.HttpContext.RequestServices,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value ?? string.Empty,
                context.ModelState);

            return new BadRequestObjectResult(response);
        };
    }

    private static async Task QueueValidationNotificationAsync(
        IServiceProvider services,
        string method,
        string path,
        ModelStateDictionary modelState)
    {
        IBackgroundTaskQueue queue = services.GetRequiredService<IBackgroundTaskQueue>();
        string[] errors = modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(error => $"{x.Key}: {error.ErrorMessage}"))
            .ToArray();

        string body = $"""
            Erro de validação recebido.

            Path: {path}
            Method: {method}
            Time: {DateTime.UtcNow:u}

            Erros:
            {string.Join(Environment.NewLine, errors)}
            """;

        await queue.EnqueueAsync(async (sp, ct) =>
        {
            IEmailService emailService = sp.GetRequiredService<IEmailService>();
            await emailService.SendErrorNotificationAsync("[wedding.gift] Erro de validação", body, ct);
        }, CancellationToken.None);
    }
}
