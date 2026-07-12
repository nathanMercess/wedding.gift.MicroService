using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Implementations;
using Xunit;

namespace wedding.gift.Tests;

public class CoupleServiceTests
{
    [Fact]
    public async Task UpdateAsync_DevePersistirAlteracoesERetornarDadosSalvos()
    {
        AppDbContext context = CreateContext();
        CoupleService service = CreateService(context);
        DateTime weddingDate = new(2026, 10, 24, 19, 30, 0, DateTimeKind.Utc);

        CoupleResponseDto updated = await service.UpdateAsync(new CoupleUpdateDto
        {
            Names = "Ana & Bruno",
            WeddingDate = weddingDate,
            PhotoUrl = "https://cdn.site.com/casal.jpg",
            Message = "Bem-vindos ao nosso site",
            EventLocation = "Espaço Jardim",
            PrimaryColor = "#123456",
            SecondaryColor = "#ABCDEF",
            GiftDisplayMode = GiftDisplayModes.PrivateUnlimited,
            CarouselPhotos =
            [
                new CarouselPhotoDto
                {
                    Url = " https://cdn.site.com/foto1.jpg ",
                    Tag = "entrada",
                    Title = "Entrada"
                }
            ]
        }, CancellationToken.None);

        Couple persisted = await context.Couples.AsNoTracking().SingleAsync();

        Assert.Equal("Ana & Bruno", persisted.Names);
        Assert.Equal(weddingDate, persisted.WeddingDate);
        Assert.Equal("https://cdn.site.com/casal.jpg", persisted.PhotoUrl);
        Assert.Equal("Bem-vindos ao nosso site", persisted.Message);
        Assert.Equal("Espaço Jardim", persisted.EventLocation);
        Assert.Equal("#123456", persisted.PrimaryColor);
        Assert.Equal("#ABCDEF", persisted.SecondaryColor);
        Assert.Equal(GiftDisplayModes.PrivateUnlimited, persisted.GiftDisplayMode);
        Assert.NotNull(persisted.CarouselPhotosJson);
        Assert.Contains("https://cdn.site.com/foto1.jpg", persisted.CarouselPhotosJson);

        Assert.Equal(persisted.Id, updated.Id);
        Assert.Equal(persisted.Names, updated.Names);
        Assert.Equal(persisted.PrimaryColor, updated.PrimaryColor);
        Assert.Single(updated.CarouselPhotos);
        Assert.Equal("https://cdn.site.com/foto1.jpg", updated.CarouselPhotos[0].Url);
    }

    [Fact]
    public async Task GetAsync_DeveBuscarBancoEmTodaChamada_ParaEvitarSiteSettingsStale()
    {
        AppDbContext context = CreateContext();
        Couple couple = Couple.Create();
        couple.Update("Ana & Bruno", DateTime.UtcNow, string.Empty, "Mensagem antiga", string.Empty, "#111111", "#222222", GiftDisplayModes.Traditional, null, null);
        context.Couples.Add(couple);
        await context.SaveChangesAsync();

        CoupleService service = CreateService(context);

        CoupleResponseDto first = await service.GetAsync(CancellationToken.None);
        Assert.Equal("Mensagem antiga", first.Message);

        couple.Update("Ana & Bruno", DateTime.UtcNow, string.Empty, "Mensagem nova", string.Empty, "#333333", "#444444", GiftDisplayModes.PrivateUnlimited, null, null);
        await context.SaveChangesAsync();

        CoupleResponseDto second = await service.GetAsync(CancellationToken.None);

        Assert.Equal("Mensagem nova", second.Message);
        Assert.Equal("#333333", second.PrimaryColor);
        Assert.Equal(GiftDisplayModes.PrivateUnlimited, second.GiftDisplayMode);
    }


    [Fact]
    public async Task GetAsync_DeveRetornarSiteSettingsDefault_QuandoNaoPersistido()
    {
        AppDbContext context = CreateContext();
        Couple couple = Couple.Create();
        couple.Update("Ana & Bruno", DateTime.UtcNow, string.Empty, string.Empty, string.Empty, "#111111", "#222222", GiftDisplayModes.Traditional, null, null);
        context.Couples.Add(couple);
        await context.SaveChangesAsync();

        CoupleResponseDto result = await CreateService(context).GetAsync(CancellationToken.None);

        Assert.NotNull(result.SiteSettings);
        Assert.True(result.SiteSettings.ShowCountdown);
        Assert.Equal(["Cozinha", "Eletrodomésticos", "Quarto", "Mesa", "Casa"], result.SiteSettings.EnabledCategories);
        Assert.Equal("Escolha seu presente", result.SiteSettings.GiftSectionTitle);
    }

    [Fact]
    public async Task UpdateAsync_DeveMesclarSiteSettingsParcial_ComConfiguracaoAtual()
    {
        AppDbContext context = CreateContext();
        CoupleService service = CreateService(context);

        await service.UpdateAsync(new CoupleUpdateDto
        {
            Names = "Ana & Bruno",
            WeddingDate = DateTime.UtcNow,
            SiteSettings = new SiteSettingsUpdateDto
            {
                ShowCountdown = false,
                EnabledCategories = ["Casa", "Mesa"],
                GiftSectionTitle = "Lista especial"
            }
        }, CancellationToken.None);

        CoupleResponseDto updated = await service.UpdateAsync(new CoupleUpdateDto
        {
            Names = "Ana & Bruno",
            WeddingDate = DateTime.UtcNow,
            SiteSettings = new SiteSettingsUpdateDto { GiftSectionSubtitle = "Escolha com carinho" }
        }, CancellationToken.None);

        Assert.False(updated.SiteSettings.ShowCountdown);
        Assert.Equal(["Casa", "Mesa"], updated.SiteSettings.EnabledCategories);
        Assert.Equal("Lista especial", updated.SiteSettings.GiftSectionTitle);
        Assert.Equal("Escolha com carinho", updated.SiteSettings.GiftSectionSubtitle);
    }

    [Fact]
    public async Task UpdateAsync_DeveRejeitarCategoriaInvalidaEmSiteSettings()
    {
        AppDbContext context = CreateContext();
        CoupleService service = CreateService(context);

        await Assert.ThrowsAsync<wedding.gift.Services.Implementations.Exceptions.BadRequestException>(() => service.UpdateAsync(new CoupleUpdateDto
        {
            Names = "Ana & Bruno",
            WeddingDate = DateTime.UtcNow,
            SiteSettings = new SiteSettingsUpdateDto { EnabledCategories = ["Invalida"] }
        }, CancellationToken.None));
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CoupleService CreateService(AppDbContext context)
    {
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        return new(new CoupleRepository(context), new ApplicationCacheService(cache));
    }
}
