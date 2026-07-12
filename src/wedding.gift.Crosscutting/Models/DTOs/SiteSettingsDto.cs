namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class SiteSettingsDto
{
    public bool ShowCountdown { get; set; } = true;
    public bool ShowEventLocation { get; set; } = true;
    public bool ShowCoupleMessage { get; set; } = true;
    public bool ShowGuestStats { get; set; } = true;
    public bool ShowGiftCategories { get; set; } = true;
    public bool ShowGiftProgress { get; set; } = true;
    public bool ShowContributionType { get; set; } = true;
    public bool ShowCategoryFilter { get; set; } = true;
    public bool ShowPriceFilter { get; set; } = true;
    public bool ShowAvailabilityFilter { get; set; } = true;
    public List<string> EnabledCategories { get; set; } = [];
    public string GiftSectionTitle { get; set; } = string.Empty;
    public string GiftSectionSubtitle { get; set; } = string.Empty;
    public string SearchPlaceholder { get; set; } = string.Empty;
    public string PresentButtonLabel { get; set; } = string.Empty;
    public string EmptyStateTitle { get; set; } = string.Empty;
    public string EmptyStateMessage { get; set; } = string.Empty;
}
