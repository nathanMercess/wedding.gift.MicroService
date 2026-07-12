using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Services.Contracts;

namespace wedding.gift.Services.Implementations;

public sealed class ApiRequestLogService(IApiRequestLogRepository apiRequestLogRepository) : IApiRequestLogService
{
    public async Task SaveAsync(ApiRequestLogCreateDto dto, CancellationToken cancellationToken)
    {
        ApiRequestLog requestLog = ApiRequestLog.Create(
            dto.StartedAtUtc,
            dto.CompletedAtUtc,
            dto.DurationMilliseconds,
            dto.Method,
            dto.Path,
            dto.QueryString,
            dto.EndpointName,
            dto.StatusCode,
            dto.IsAuthenticated,
            dto.UserId,
            dto.UserRole,
            dto.ClientIp,
            dto.UserAgent,
            dto.CorrelationId,
            dto.ExceptionType,
            dto.ExceptionMessage);

        await apiRequestLogRepository.AddAsync(requestLog, cancellationToken);
        await apiRequestLogRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CleanupAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
        => await apiRequestLogRepository.DeleteOlderThanAsync(cutoffUtc, cancellationToken);
}
