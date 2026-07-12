using System.ComponentModel.DataAnnotations;

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
    [MaxLength(20)]
    public List<string>? EnabledCategories { get; set; }
    [MaxLength(120)]
    public string? GiftSectionTitle { get; set; }
    [MaxLength(300)]
    public string? GiftSectionSubtitle { get; set; }
    [MaxLength(120)]
    public string? SearchPlaceholder { get; set; }
    [MaxLength(80)]
    public string? PresentButtonLabel { get; set; }
    [MaxLength(120)]
    public string? EmptyStateTitle { get; set; }
    [MaxLength(300)]
    public string? EmptyStateMessage { get; set; }
}
