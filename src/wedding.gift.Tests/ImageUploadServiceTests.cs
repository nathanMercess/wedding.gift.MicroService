using Microsoft.Extensions.Options;
using wedding.gift.Crosscutting.Models.Configurations;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using Xunit;

namespace wedding.gift.Tests;

public sealed class ImageUploadServiceTests
{
    [Fact]
    public async Task UploadImageAsync_DeveRejeitarConteudoQueNaoCorrespondeAExtensao()
    {
        ImageUploadService service = new(
            null!,
            Options.Create(new GcsOptions { BucketName = "test-bucket" }));
        await using MemoryStream content = new("not-a-png"u8.ToArray());

        await Assert.ThrowsAsync<BadRequestException>(() => service.UploadImageAsync(
            content,
            "image.png",
            "image/png",
            content.Length,
            CancellationToken.None));
    }
}
