namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class SiteSettingsUpdateDto
{
    public bool? ShowCountdown { get; set; }
    public bool? ShowEventLocation { get; set; }
    public bool? ShowCoupleMessage { get; set; }
    public bool? ShowGuestStats { get; set; }
    public bool? ShowGiftCategories { get; set; }
    public bool? ShowGiftProgress { get; set; }
    public bool? ShowContributionType { get; set; }
    public bool? ShowCategoryFilter { get; set; }
    public bool? ShowPriceFilter { get; set; }
    public bool? ShowAvailabilityFilter { get; set; }
    public List<string>? EnabledCategories { get; set; }
    public string? GiftSectionTitle { get; set; }
    public string? GiftSectionSubtitle { get; set; }
    public string? SearchPlaceholder { get; set; }
    public string? PresentButtonLabel { get; set; }
    public string? EmptyStateTitle { get; set; }
    public string? EmptyStateMessage { get; set; }
}
