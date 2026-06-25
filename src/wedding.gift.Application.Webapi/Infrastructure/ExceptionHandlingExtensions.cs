using Microsoft.AspNetCore.Diagnostics;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Infrastructure;

public static class ExceptionHandlingExtensions
{
    public static WebApplication UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(HandleExceptionAsync);
        });

        return app;
    }

    private static async Task HandleExceptionAsync(HttpContext context)
    {
        Exception exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        ILogger<Program> logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        string correlationId = context.TraceIdentifier;

        LogException(exception, context, logger, correlationId);

        if (exception is not null) await QueueErrorNotificationAsync(context, exception, correlationId);

        ApiResponseDto<object> response = CreateErrorResponse(exception, correlationId);
        int statusCode = exception is AppException appException
            ? appException.StatusCode
            : StatusCodes.Status500InternalServerError;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private static void LogException(Exception exception, HttpContext context, ILogger<Program> logger, string correlationId)
    {
        if (exception is AppException appException)
        {
            logger.LogWarning(
                exception,
                "Erro de negocio {Status} em {Path}. CorrelationId={CorrelationId}",
                appException.StatusCode,
                context.Request.Path,
                correlationId);

            return;
        }

        if (exception is not null)
        {
            logger.LogError(
                exception,
                "Erro nao tratado em {Path}. CorrelationId={CorrelationId}",
                context.Request.Path,
                correlationId);
        }
    }

    private static async Task QueueErrorNotificationAsync(HttpContext context, Exception exception, string correlationId)
    {
        IBackgroundTaskQueue queue = context.RequestServices.GetRequiredService<IBackgroundTaskQueue>();
        string subject = $"[wedding.gift] {exception.GetType().Name}: {exception.Message}";
        string body = $"CorrelationId: {correlationId}\nPath: {context.Request.Path}\n" +
                      $"Method: {context.Request.Method}\nTime: {DateTime.UtcNow:u}\n\n{exception}";

        await queue.EnqueueAsync(async (sp, ct) =>
        {
            IEmailService emailService = sp.GetRequiredService<IEmailService>();
            await emailService.SendErrorNotificationAsync(subject, body, ct);
        }, CancellationToken.None);
    }

    private static ApiResponseDto<object> CreateErrorResponse(Exception exception, string correlationId)
    {
        string code = exception switch
        {
            AppException appException => appException.Code,
            _ => ErrorCodes.UNHANDLED_ERROR
        };

        return ApiResponseDto<object>.Fail(code, correlationId);
    }
}
