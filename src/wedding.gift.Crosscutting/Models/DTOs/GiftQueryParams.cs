namespace wedding.gift.Crosscutting.Models.DTOs;

public class GiftQueryParams
{
    public string? Search { get; set; }
    public string? Category { get; set; }
    public string OrderBy { get; set; } = "name";
    public string OrderDir { get; set; } = "asc";
    public bool? OnlyAvailable { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
