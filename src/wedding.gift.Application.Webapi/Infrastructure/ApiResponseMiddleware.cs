using System.Text.Json;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class ApiResponseMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldSkip(context))
        {
            await next(context);
            return;
        }

        Stream originalBody = context.Response.Body;

        await using MemoryStream responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await next(context);
        }
        catch
        {
            context.Response.Body = originalBody;
            throw;
        }

        context.Response.Body = originalBody;

        if (!ShouldWrap(context, responseBody))
        {
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBody, context.RequestAborted);
            return;
        }

        await WriteWrappedResponseAsync(context, responseBody);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/swagger") ||
               context.Request.Path.StartsWithSegments("/health");
    }

    private static bool ShouldWrap(HttpContext context, MemoryStream responseBody)
    {
        int statusCode = context.Response.StatusCode;

        if (statusCode == StatusCodes.Status204NoContent || statusCode == StatusCodes.Status304NotModified)
        {
            return false;
        }

        if (responseBody.Length == 0)
        {
            return statusCode >= 200;
        }

        return IsJsonResponse(context.Response.ContentType);
    }

    private static async Task WriteWrappedResponseAsync(HttpContext context, MemoryStream responseBody)
    {
        if (responseBody.Length == 0)
        {
            await WriteEmptyWrappedResponseAsync(context);
            return;
        }

        responseBody.Position = 0;

        JsonDocument document;

        try
        {
            document = await JsonDocument.ParseAsync(responseBody, cancellationToken: context.RequestAborted);
        }
        catch (JsonException)
        {
            responseBody.Position = 0;
            await responseBody.CopyToAsync(context.Response.Body, context.RequestAborted);
            return;
        }

        using (document)
        {
            if (IsAlreadyWrapped(document.RootElement))
            {
                responseBody.Position = 0;
                await responseBody.CopyToAsync(context.Response.Body, context.RequestAborted);
                return;
            }

            context.Response.ContentType = "application/json";
            context.Response.ContentLength = null;

            if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            {
                string code = ExtractErrorCode(document.RootElement, context.Response.StatusCode);
                ApiResponseDto<object> failure = ApiResponseDto<object>.Fail(code, context.TraceIdentifier);
                await context.Response.WriteAsJsonAsync(failure, cancellationToken: context.RequestAborted);
                return;
            }

            JsonElement data = document.RootElement.Clone();
            ApiResponseDto<JsonElement> success = ApiResponseDto<JsonElement>.Ok(data, context.TraceIdentifier);
            await context.Response.WriteAsJsonAsync(success, cancellationToken: context.RequestAborted);
        }
    }

    private static async Task WriteEmptyWrappedResponseAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json";
        context.Response.ContentLength = null;

        if (context.Response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            string code = GetDefaultErrorCode(context.Response.StatusCode);
            ApiResponseDto<object> failure = ApiResponseDto<object>.Fail(code, context.TraceIdentifier);
            await context.Response.WriteAsJsonAsync(failure, cancellationToken: context.RequestAborted);
            return;
        }

        ApiResponseDto<object> success = ApiResponseDto<object>.Ok(null, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(success, cancellationToken: context.RequestAborted);
    }

    private static bool IsJsonResponse(string contentType)
    {
        return string.IsNullOrWhiteSpace(contentType) ||
               contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlreadyWrapped(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("success", out _) &&
               root.TryGetProperty("correlationId", out _) &&
               (root.TryGetProperty("data", out _) || root.TryGetProperty("error", out _));
    }

    private static string ExtractErrorCode(JsonElement root, int statusCode)
    {
        if (TryGetStringProperty(root, "errorCode", out string errorCode)) return errorCode;
        if (TryGetStringProperty(root, "code", out string code)) return code;
        if (TryGetStringProperty(root, "title", out string title)) return title;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("error", out JsonElement error) &&
            TryGetStringProperty(error, "code", out string nestedCode))
        {
            return nestedCode;
        }

        return GetDefaultErrorCode(statusCode);
    }

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string GetDefaultErrorCode(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => ErrorCodes.BAD_REQUEST,
            StatusCodes.Status401Unauthorized => ErrorCodes.UNAUTHORIZED,
            StatusCodes.Status403Forbidden => ErrorCodes.FORBIDDEN,
            StatusCodes.Status404NotFound => ErrorCodes.NOT_FOUND,
            StatusCodes.Status500InternalServerError => ErrorCodes.UNHANDLED_ERROR,
            _ => ErrorCodes.HTTP_ERROR
        };
    }
}
