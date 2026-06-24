using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs;

public class DashboardQueryDto
{
    [Range(1, 365, ErrorMessage = "O periodo deve estar entre 1 e 365 dias.")]
    public int Days { get; set; } = 30;

    [Range(1, 50, ErrorMessage = "A quantidade de itens recentes deve estar entre 1 e 50.")]
    public int RecentItems { get; set; } = 10;
}
