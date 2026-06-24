using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Application.Webapi.Infrastructure;

public static class ApiBehaviorOptionsExtensions
{
    public static void UseValidationProblemDetails(this ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            ValidationProblemDetails details = new ValidationProblemDetails(context.ModelState)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Erro de validação",
                Detail = "Verifique os campos enviados e tente novamente."
            };

            _ = QueueValidationNotificationAsync(
                context.HttpContext.RequestServices,
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value ?? string.Empty,
                context.ModelState);

            return new BadRequestObjectResult(details);
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
        });
    }
}
