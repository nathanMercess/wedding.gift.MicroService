using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Domain.Model.Entities;

public sealed class Couple
{
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private Couple()
    {
    }

    public Guid Id { get; private set; }
    public string Names { get; private set; } = string.Empty;
    public DateTime WeddingDate { get; private set; }
    public string PhotoUrl { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string EventLocation { get; private set; } = string.Empty;
    public string PrimaryColor { get; private set; } = "#C79A6D";
    public string SecondaryColor { get; private set; } = "#F7F0EA";
    public string GiftDisplayMode { get; private set; } = GiftDisplayModes.Traditional;
    public string? CarouselPhotosJson { get; private set; }
    public string? SiteSettingsJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Couple Create(Guid? id = null)
    {
        DateTime now = DateTime.UtcNow;

        return new Couple
        {
            Id = id ?? SingletonId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string names,
        DateTime weddingDate,
        string photoUrl,
        string message,
        string eventLocation,
        string? primaryColor,
        string? secondaryColor,
        string? giftDisplayMode,
        string? carouselPhotosJson,
        string? siteSettingsJson = null)
    {
        Names = names.Trim();
        WeddingDate = weddingDate;
        PhotoUrl = photoUrl.Trim();
        Message = message.Trim();
        EventLocation = eventLocation.Trim();
        PrimaryColor = string.IsNullOrWhiteSpace(primaryColor) ? "#C79A6D" : primaryColor.Trim();
        SecondaryColor = string.IsNullOrWhiteSpace(secondaryColor) ? "#F7F0EA" : secondaryColor.Trim();
        GiftDisplayMode = GiftDisplayModes.Normalize(giftDisplayMode);
        CarouselPhotosJson = carouselPhotosJson;
        SiteSettingsJson = siteSettingsJson;
        Touch();
    }

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
