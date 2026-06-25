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

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
        {
            throw new BadRequestException(ErrorCodes.INVALID_IMAGE_CONTENT_TYPE);
        }

        var bucketName = gcsOptions.Value.BucketName;
        var objectName = $"gifts/{Guid.NewGuid()}{extension}";

        var uploaded = await storageClient.UploadObjectAsync(
            bucketName,
            objectName,
            contentType,
            content,
            cancellationToken: cancellationToken);

        return $"https://storage.googleapis.com/{bucketName}/{uploaded.Name}";
    }
}
