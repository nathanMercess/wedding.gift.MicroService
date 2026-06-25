namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class GiftQueryParams
{
    public string? Search { get; set; }
    public string? Category { get; set; }

    // Agora usando os Enums com valores padrão
    public GiftSortField OrderBy { get; set; } = GiftSortField.Name;
    public SortDirection OrderDir { get; set; } = SortDirection.Asc;

    public bool? OnlyAvailable { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}