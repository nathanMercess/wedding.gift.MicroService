namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class ApiErrorDto
{
    public string Code { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string[]> Fields { get; set; }
    public object Details { get; set; }
}
