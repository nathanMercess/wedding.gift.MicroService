namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class DashboardGiftInsightsDto
{
    public int Total { get; set; }
    public int FullyFunded { get; set; }
    public int Available { get; set; }
    public int FullyFundedButAvailable { get; set; }
    public int WithoutContributions { get; set; }
    public int Overfunded { get; set; }
    public List<DashboardGiftFundingDto> TopRemainingGifts { get; set; } = [];
    public List<DashboardGiftFundingDto> TopRaisedGifts { get; set; } = [];
    public List<DashboardGiftFundingDto> StalledGifts { get; set; } = [];
}
