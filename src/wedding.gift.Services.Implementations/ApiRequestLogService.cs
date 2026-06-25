using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class ApiRequestLogService(AppDbContext dbContext) : IApiRequestLogService
{
    public async Task SaveAsync(ApiRequestLogCreateDto dto, CancellationToken cancellationToken)
    {
        var statusCode = dto.StatusCode <= 0 ? 500 : dto.StatusCode;

        dbContext.ApiRequestLogs.Add(new ApiRequestLog
        {
            Id = Guid.NewGuid(),
            StartedAtUtc = dto.StartedAtUtc,
            CompletedAtUtc = dto.CompletedAtUtc,
            DurationMilliseconds = dto.DurationMilliseconds,
            Method = Trim(dto.Method, 16),
            Path = Trim(dto.Path, 500),
            QueryString = Trim(dto.QueryString, 1000),
            EndpointName = Trim(dto.EndpointName, 300),
            StatusCode = statusCode,
            IsSuccess = statusCode < 400,
            IsAuthenticated = dto.IsAuthenticated,
            UserId = Trim(dto.UserId, 64),
            UserRole = Trim(dto.UserRole, 80),
            ClientIp = Trim(dto.ClientIp, 64),
            UserAgent = Trim(dto.UserAgent, 500),
            CorrelationId = Trim(dto.CorrelationId, 100),
            ExceptionType = Trim(dto.ExceptionType, 200),
            ExceptionMessage = Trim(dto.ExceptionMessage, 500)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
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
