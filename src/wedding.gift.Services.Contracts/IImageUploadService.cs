namespace wedding.gift.Services.Contracts;

public interface IImageUploadService
{
    Task<string> UploadImageAsync(Stream content, string fileName, string contentType, long length, CancellationToken cancellationToken);
}
