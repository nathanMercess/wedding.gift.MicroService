using wedding.gift.Crosscutting.Constants;

namespace wedding.gift.Crosscutting.Models.DTOs;

public sealed class CoupleResponseDto
{
    public Guid Id { get; set; }
    public string Names { get; set; } = string.Empty;
    public DateTime WeddingDate { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#C79A6D";
    public string SecondaryColor { get; set; } = "#F7F0EA";
    public string GiftDisplayMode { get; set; } = GiftDisplayModes.Traditional;
    public List<CarouselPhotoDto> CarouselPhotos { get; set; } = [];
    public SiteSettingsDto SiteSettings { get; set; } = new();
}
