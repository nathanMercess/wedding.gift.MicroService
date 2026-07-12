using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftQueryParams
{
    [MaxLength(120)]
    public string? Search { get; set; }
    [MaxLength(80)]
    public string? Category { get; set; }
    [Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? MinTotal { get; set; }
    [Range(typeof(decimal), "0", "99999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal? MaxTotal { get; set; }

    public GiftSortField OrderBy { get; set; } = GiftSortField.Name;
    public SortDirection OrderDir { get; set; } = SortDirection.Asc;

    public bool? OnlyAvailable { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
