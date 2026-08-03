namespace wedding.gift.Domain.Model.Entities;

public sealed class ApiRequestLog
{
    private ApiRequestLog()
    {
    }

    public Guid Id { get; private init; }
    public DateTime StartedAtUtc { get; private init; }
    public DateTime CompletedAtUtc { get; private init; }
    public long DurationMilliseconds { get; private init; }
    public string Method { get; private init; } = string.Empty;
    public string Path { get; private init; } = string.Empty;
    public string QueryString { get; private init; } = string.Empty;
    public string EndpointName { get; private init; } = string.Empty;
    public int StatusCode { get; private init; }
    public bool IsSuccess { get; private init; }
    public bool IsAuthenticated { get; private init; }
    public string UserId { get; private init; } = string.Empty;
    public string UserRole { get; private init; } = string.Empty;
    public string ClientIp { get; private init; } = string.Empty;
    public string UserAgent { get; private init; } = string.Empty;
    public string CorrelationId { get; private init; } = string.Empty;
    public string ExceptionType { get; private init; } = string.Empty;
    public string ExceptionMessage { get; private init; } = string.Empty;

    public static ApiRequestLog Create(
        DateTime startedAtUtc,
        DateTime completedAtUtc,
        long durationMilliseconds,
        string method,
        string path,
        string queryString,
        string endpointName,
        int statusCode,
        bool isAuthenticated,
        string userId,
        string userRole,
        string clientIp,
        string userAgent,
        string correlationId,
        string exceptionType,
        string exceptionMessage)
    {
        int normalizedStatusCode = statusCode <= 0 ? 500 : statusCode;

        return new ApiRequestLog
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            DurationMilliseconds = durationMilliseconds,
            Method = Trim(method, 16),
            Path = Trim(path, 500),
            QueryString = Trim(queryString, 1000),
            EndpointName = Trim(endpointName, 300),
            StatusCode = normalizedStatusCode,
            IsSuccess = normalizedStatusCode < 400,
            IsAuthenticated = isAuthenticated,
            UserId = Trim(userId, 64),
            UserRole = Trim(userRole, 80),
            ClientIp = Trim(clientIp, 64),
            UserAgent = Trim(userAgent, 500),
            CorrelationId = Trim(correlationId, 100),
            ExceptionType = Trim(exceptionType, 200),
            ExceptionMessage = Trim(exceptionMessage, 500)
        };
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
