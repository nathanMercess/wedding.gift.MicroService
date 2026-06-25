namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardApiRequestActivityDto
{
    public Guid Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public bool IsAuthenticated { get; set; }
    public string UserRole { get; set; } = string.Empty;
    public long DurationMilliseconds { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string ExceptionType { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
}
