using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class UserQueryParams
{
    [MaxLength(120)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
