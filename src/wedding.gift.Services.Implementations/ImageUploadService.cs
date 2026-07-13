using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations.Exceptions;

namespace wedding.gift.Services.Implementations;

public sealed class ImageUploadService(StorageClient storageClient, IOptions<GcsOptions> gcsOptions) : IImageUploadService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly HashSet<string> AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxFileSizeBytes = 20 * 1024 * 1024;

    public async Task<string> UploadImageAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);
        }

        if (length > MaxFileSizeBytes)
        {
            throw new BadRequestException(ErrorCodes.IMAGE_FILE_TOO_LARGE);
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_CONTENT_TYPE);
        }

        if (!content.CanSeek)
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        long originalPosition = content.Position;
        byte[] header = new byte[12];
        int bytesRead = await content.ReadAsync(header.AsMemory(), cancellationToken);
        content.Position = originalPosition;

        if (!HasValidSignature(extension, header.AsSpan(0, bytesRead)))
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        string bucketName = gcsOptions.Value.BucketName;
        string objectName = $"gifts/{Guid.NewGuid()}{extension}";

        Google.Apis.Storage.v1.Data.Object destination = new()
        {
            Bucket = bucketName,
            Name = objectName,
            ContentType = contentType,
            CacheControl = "public, max-age=31536000, immutable"
        };
        Google.Apis.Storage.v1.Data.Object uploaded = await storageClient.UploadObjectAsync(
            destination,
            content,
            cancellationToken: cancellationToken);

        return $"https://storage.googleapis.com/{bucketName}/{uploaded.Name}";
    }

    public async Task DeleteImageAsync(string url, CancellationToken cancellationToken)
    {
        string bucketName = gcsOptions.Value.BucketName;
        string expectedPrefix = $"https://storage.googleapis.com/{bucketName}/gifts/";

        if (!url.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        string objectName = Uri.UnescapeDataString(url[$"https://storage.googleapis.com/{bucketName}/".Length..]);

        if (string.IsNullOrWhiteSpace(objectName) || objectName.Contains("..", StringComparison.Ordinal))
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_FILE);

        await storageClient.DeleteObjectAsync(bucketName, objectName, cancellationToken: cancellationToken);
    }

    private static bool HasValidSignature(string extension, ReadOnlySpan<byte> header)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 &&
                                   header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header.Length >= 8 &&
                      header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => header.Length >= 12 &&
                       header[..4].SequenceEqual("RIFF"u8) &&
                       header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
