using System.Text.Json;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations.Extensions;

public static class SiteSettingsExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static SiteSettingsDto Defaults()
        => new()
        {
            EnabledCategories = GiftCategories.All.ToList(),
            GiftSectionTitle = "Escolha seu presente",
            GiftSectionSubtitle = string.Empty,
            SearchPlaceholder = "Buscar presente...",
            PresentButtonLabel = "Presentear",
            EmptyStateTitle = "Nenhum presente encontrado",
            EmptyStateMessage = "Tente ajustar os filtros ou buscar por outro termo"
        };

    public static SiteSettingsDto Normalize(string? json)
        => Normalize(Deserialize(json));

    public static SiteSettingsDto Merge(string? currentJson, SiteSettingsUpdateDto? incoming)
    {
        SiteSettingsDto current = Normalize(currentJson);
        return incoming is null ? current : Normalize(incoming, current);
    }

    public static string ToSiteSettingsJson(this SiteSettingsDto settings)
        => JsonSerializer.Serialize(Normalize(settings), JsonOptions);

    private static SiteSettingsDto Normalize(SiteSettingsDto? settings, SiteSettingsDto? fallback = null)
    {
        SiteSettingsDto defaults = fallback ?? Defaults();
        settings ??= defaults;
        List<string> categories = settings.EnabledCategories ?? defaults.EnabledCategories;
        ValidateCategories(categories);

        return new SiteSettingsDto
        {
            ShowCountdown = settings.ShowCountdown,
            ShowEventLocation = settings.ShowEventLocation,
            ShowCoupleMessage = settings.ShowCoupleMessage,
            ShowGuestStats = settings.ShowGuestStats,
            ShowGiftCategories = settings.ShowGiftCategories,
            ShowGiftProgress = settings.ShowGiftProgress,
            ShowContributionType = settings.ShowContributionType,
            ShowCategoryFilter = settings.ShowCategoryFilter,
            ShowPriceFilter = settings.ShowPriceFilter,
            ShowAvailabilityFilter = settings.ShowAvailabilityFilter,
            EnabledCategories = NormalizeCategories(categories),
            GiftSectionTitle = string.IsNullOrWhiteSpace(settings.GiftSectionTitle) ? defaults.GiftSectionTitle : settings.GiftSectionTitle.Trim(),
            GiftSectionSubtitle = settings.GiftSectionSubtitle?.Trim() ?? defaults.GiftSectionSubtitle,
            SearchPlaceholder = string.IsNullOrWhiteSpace(settings.SearchPlaceholder) ? defaults.SearchPlaceholder : settings.SearchPlaceholder.Trim(),
            PresentButtonLabel = string.IsNullOrWhiteSpace(settings.PresentButtonLabel) ? defaults.PresentButtonLabel : settings.PresentButtonLabel.Trim(),
            EmptyStateTitle = string.IsNullOrWhiteSpace(settings.EmptyStateTitle) ? defaults.EmptyStateTitle : settings.EmptyStateTitle.Trim(),
            EmptyStateMessage = string.IsNullOrWhiteSpace(settings.EmptyStateMessage) ? defaults.EmptyStateMessage : settings.EmptyStateMessage.Trim()
        };
    }

    private static SiteSettingsDto Normalize(SiteSettingsUpdateDto incoming, SiteSettingsDto current)
    {
        List<string> categories = incoming.EnabledCategories ?? current.EnabledCategories;
        ValidateCategories(categories);

        return new SiteSettingsDto
        {
            ShowCountdown = incoming.ShowCountdown ?? current.ShowCountdown,
            ShowEventLocation = incoming.ShowEventLocation ?? current.ShowEventLocation,
            ShowCoupleMessage = incoming.ShowCoupleMessage ?? current.ShowCoupleMessage,
            ShowGuestStats = incoming.ShowGuestStats ?? current.ShowGuestStats,
            ShowGiftCategories = incoming.ShowGiftCategories ?? current.ShowGiftCategories,
            ShowGiftProgress = incoming.ShowGiftProgress ?? current.ShowGiftProgress,
            ShowContributionType = incoming.ShowContributionType ?? current.ShowContributionType,
            ShowCategoryFilter = incoming.ShowCategoryFilter ?? current.ShowCategoryFilter,
            ShowPriceFilter = incoming.ShowPriceFilter ?? current.ShowPriceFilter,
            ShowAvailabilityFilter = incoming.ShowAvailabilityFilter ?? current.ShowAvailabilityFilter,
            EnabledCategories = NormalizeCategories(categories),
            GiftSectionTitle = incoming.GiftSectionTitle is null ? current.GiftSectionTitle : NormalizeText(incoming.GiftSectionTitle, current.GiftSectionTitle),
            GiftSectionSubtitle = incoming.GiftSectionSubtitle is null ? current.GiftSectionSubtitle : incoming.GiftSectionSubtitle.Trim(),
            SearchPlaceholder = incoming.SearchPlaceholder is null ? current.SearchPlaceholder : NormalizeText(incoming.SearchPlaceholder, current.SearchPlaceholder),
            PresentButtonLabel = incoming.PresentButtonLabel is null ? current.PresentButtonLabel : NormalizeText(incoming.PresentButtonLabel, current.PresentButtonLabel),
            EmptyStateTitle = incoming.EmptyStateTitle is null ? current.EmptyStateTitle : NormalizeText(incoming.EmptyStateTitle, current.EmptyStateTitle),
            EmptyStateMessage = incoming.EmptyStateMessage is null ? current.EmptyStateMessage : NormalizeText(incoming.EmptyStateMessage, current.EmptyStateMessage)
        };
    }

    private static void ValidateCategories(IEnumerable<string> categories)
    {
        string? invalidCategory = categories.FirstOrDefault(x => !GiftCategories.IsValid(x));

        if (invalidCategory is not null)
            throw new BadRequestException(ErrorCodes.VALIDATION_ERROR);
    }

    private static List<string> NormalizeCategories(IEnumerable<string> categories)
        => categories.Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static string NormalizeText(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static SiteSettingsDto? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SiteSettingsDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
