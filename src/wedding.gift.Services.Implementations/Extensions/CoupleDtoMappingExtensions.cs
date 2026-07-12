using System.Text.Json;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Services.Implementations.Extensions;

public static class CoupleDtoMappingExtensions
{
    public static CoupleResponseDto ToResponseDto(this Couple entity)
        => new()
        {
            Id = entity.Id,
            Names = entity.Names,
            WeddingDate = entity.WeddingDate,
            PhotoUrl = entity.PhotoUrl,
            Message = entity.Message,
            EventLocation = entity.EventLocation,
            PrimaryColor = entity.PrimaryColor,
            SecondaryColor = entity.SecondaryColor,
            GiftDisplayMode = entity.GiftDisplayMode,
            CarouselPhotos = DeserializeCarouselPhotos(entity.CarouselPhotosJson),
            SiteSettings = SiteSettingsExtensions.Normalize(entity.SiteSettingsJson)
        };

    public static string? ToCarouselPhotosJson(this CoupleUpdateDto dto)
    {
        List<CarouselPhotoDto> photos = dto.CarouselPhotos?
            .Where(p => !string.IsNullOrWhiteSpace(p.Url))
            .Select(p => new CarouselPhotoDto { Url = p.Url.Trim(), Tag = p.Tag.Trim(), Title = p.Title.Trim() })
            .ToList();

        return photos is { Count: > 0 } ? JsonSerializer.Serialize(photos) : null;
    }

    private static List<CarouselPhotoDto> DeserializeCarouselPhotos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return [];

        List<CarouselPhotoDto> result = new();

        foreach (JsonElement element in root.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
                result.Add(new CarouselPhotoDto { Url = element.GetString() ?? string.Empty });
            else if (element.ValueKind == JsonValueKind.Object)
                result.Add(new CarouselPhotoDto
                {
                    Url = element.TryGetProperty("Url", out JsonElement url) ? url.GetString() ?? string.Empty : string.Empty,
                    Tag = element.TryGetProperty("Tag", out JsonElement tag) ? tag.GetString() ?? string.Empty : string.Empty,
                    Title = element.TryGetProperty("Title", out JsonElement title) ? title.GetString() ?? string.Empty : string.Empty
                });
        }

        return result;
    }
}
