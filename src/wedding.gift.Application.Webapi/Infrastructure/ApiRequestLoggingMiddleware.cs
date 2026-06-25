using System.Diagnostics;
using System.Security.Claims;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Application.Webapi.Infrastructure;

public sealed class ApiRequestLoggingMiddleware(RequestDelegate next, ILogger<ApiRequestLoggingMiddleware> logger)
{
    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "access_token",
        "authorization",
        "cardToken",
        "code",
        "cpf",
        "doc",
        "document",
        "payerDocNumber",
        "password",
        "secret",
        "senha",
        "token"
    };

    public async Task InvokeAsync(HttpContext context, IApiRequestLogService apiRequestLogService)
    {
        if (!ShouldLog(context))
        {
            await next(context);
            return;
        }

        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        Exception capturedException = null;

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            capturedException = ex;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await SaveLogAsync(context, apiRequestLogService, startedAtUtc, stopwatch.ElapsedMilliseconds, capturedException);
        }
    }

    private async Task SaveLogAsync(
        HttpContext context,
        IApiRequestLogService apiRequestLogService,
        DateTime startedAtUtc,
        long durationMilliseconds,
        Exception capturedException)
    {
        try
        {
            var statusCode = GetStatusCode(context, capturedException);
            var user = context.User;

            await apiRequestLogService.SaveAsync(new ApiRequestLogCreateDto
            {
                StartedAtUtc = startedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                DurationMilliseconds = durationMilliseconds,
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? string.Empty,
                QueryString = BuildSanitizedQueryString(context.Request.Query),
                EndpointName = context.GetEndpoint()?.DisplayName ?? string.Empty,
                StatusCode = statusCode,
                IsAuthenticated = user.Identity?.IsAuthenticated == true,
                UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? string.Empty,
                UserRole = string.Join(",", user.FindAll(ClaimTypes.Role).Select(x => x.Value)),
                ClientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                CorrelationId = context.TraceIdentifier,
                ExceptionType = capturedException?.GetType().Name ?? string.Empty,
                ExceptionMessage = capturedException?.Message ?? string.Empty
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao persistir log de request da API. Path={Path}, CorrelationId={CorrelationId}.",
                context.Request.Path,
                context.TraceIdentifier);
        }
    }

    private static bool ShouldLog(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api");

    private static int GetStatusCode(HttpContext context, Exception capturedException)
    {
        if (capturedException is AppException appException)
        {
            return appException.StatusCode;
        }

        if (capturedException is not null)
        {
            return StatusCodes.Status500InternalServerError;
        }

        return context.Response.StatusCode <= 0
            ? StatusCodes.Status200OK
            : context.Response.StatusCode;
    }

    private static string BuildSanitizedQueryString(IQueryCollection query)
    {
        if (query.Count == 0)
        {
            return string.Empty;
        }

        var parts = query
            .OrderBy(x => x.Key)
            .SelectMany(item =>
            {
                var key = Uri.EscapeDataString(item.Key);

                if (IsSensitiveQueryKey(item.Key))
                {
                    return [$"{key}=[redacted]"];
                }

                return item.Value
                    .Select(value => $"{key}={Uri.EscapeDataString(Trim(value, 120))}");
            });

        return "?" + string.Join("&", parts);
    }

    private static bool IsSensitiveQueryKey(string key)
    {
        if (SensitiveQueryKeys.Contains(key))
        {
            return true;
        }

        return SensitiveQueryKeys.Any(sensitiveKey => key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase));
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
