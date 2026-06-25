namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ApiResponseDto<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public ApiErrorDto Error { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    public static ApiResponseDto<T> Ok(T data, string correlationId)
    {
        return new ApiResponseDto<T>
        {
            Success = true,
            Data = data,
            CorrelationId = correlationId
        };
    }

    public static ApiResponseDto<T> Fail(
        string code,
        string correlationId,
        IReadOnlyDictionary<string, string[]> fields = null,
        object details = null)
    {
        return new ApiResponseDto<T>
        {
            Success = false,
            Error = new ApiErrorDto
            {
                Code = code,
                Fields = fields,
                Details = details
            },
            CorrelationId = correlationId
        };
    }
}
