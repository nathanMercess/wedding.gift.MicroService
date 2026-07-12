#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Extensions;

namespace wedding.gift.Tests;

public class GiftServiceTests
{
    [Fact]
    public async Task CreateAsync_DeveAceitarCategoriaOpcional_QuandoNaoInformada()
    {
        AppDbContext context = CreateContext();
        GiftService service = CreateService(context);

        GiftResponseDto created = await service.CreateAsync(new GiftCreateDto
        {
            Name = "Presente sem categoria",
            Description = "Teste",
            Price = 100m,
            Total = 100m,
            Image = "image.jpg"
        }, CancellationToken.None);

        Assert.Equal(string.Empty, created.Category);
        Assert.Equal(string.Empty, context.Gifts.Single().Category);
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarPorPrecoComoDecimal()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Dez", price: 10m);
        SeedGift(context, "Dois", price: 2m);
        SeedGift(context, "Cem", price: 100.50m);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Price,
            OrderDir = SortDirection.Asc
        }, CancellationToken.None);

        Assert.Equal([2m, 10m, 100.50m], result.Items.Select(x => x.Price));
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarPorTotalComoDecimal()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Dez", price: 10m, total: 10m);
        SeedGift(context, "Dois", price: 2m, total: 2m);
        SeedGift(context, "Cem", price: 100.50m, total: 100.50m);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Total,
            OrderDir = SortDirection.Desc
        }, CancellationToken.None);

        Assert.Equal([100.50m, 10m, 2m], result.Items.Select(x => x.Total));
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarPorValorArrecadadoComoDecimal()
    {
        AppDbContext context = CreateContext();
        Gift ten = SeedGift(context, "Dez", price: 10m);
        Gift two = SeedGift(context, "Dois", price: 2m);
        Gift hundred = SeedGift(context, "Cem", price: 100.50m);
        SeedContribution(context, ten.Id, 10m);
        SeedContribution(context, two.Id, 2m);
        SeedContribution(context, hundred.Id, 100.50m);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Raised,
            OrderDir = SortDirection.Asc
        }, CancellationToken.None);

        Assert.Equal([2m, 10m, 100.50m], result.Items.Select(x => x.Raised));
    }

    [Fact]
    public async Task GetAllAsync_DeveFiltrarPorCategoriaTotalEPaginar()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Panela", price: 100m, category: GiftCategories.Cozinha);
        SeedGift(context, "Pratos", price: 200m, category: GiftCategories.Mesa);
        SeedGift(context, "Jogo americano", price: 300m, category: GiftCategories.Mesa);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams
        {
            Category = GiftCategories.Mesa,
            MinTotal = 150m,
            MaxTotal = 350m,
            OrderBy = GiftSortField.Total,
            OrderDir = SortDirection.Asc,
            Page = 2,
            PageSize = 1
        }, CancellationToken.None);

        GiftResponseDto item = Assert.Single(result.Items);
        Assert.Equal("Jogo americano", item.Name);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(GiftCategories.Mesa, item.Category);
    }

    [Fact]
    public async Task GetAllAsync_DeveCalcularRemainingComReservasAtivas()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, "Reservado", price: 500m);
        SeedContribution(context, gift.Id, 100m);
        context.Payments.Add(Payment.CreatePix(gift.Id, gift.Name, "Ana", string.Empty, "ana@test.com", "CPF", "12345678909", "ord_active", 75m, "pending", null, "mp_active", null, string.Empty, null, DateTime.UtcNow.AddMinutes(10)));
        context.Payments.Add(Payment.CreatePix(gift.Id, gift.Name, "Bruno", string.Empty, "bruno@test.com", "CPF", "12345678909", "ord_expired", 50m, "pending", null, "mp_expired", null, string.Empty, null, DateTime.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync(CancellationToken.None);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams(), CancellationToken.None);

        GiftResponseDto item = Assert.Single(result.Items);
        Assert.Equal(100m, item.Raised);
        Assert.Equal(325m, item.Remaining);
        Assert.False(item.FullyFunded);
    }

    [Fact]
    public async Task GetPublicAsync_DeveOcultarCategoriasDesabilitadas()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Panela", price: 100m, category: GiftCategories.Cozinha);
        SeedGift(context, "Pratos", price: 200m, category: GiftCategories.Mesa);
        SeedCoupleWithSiteSettings(context, new SiteSettingsDto
        {
            ShowGiftCategories = true,
            EnabledCategories = [GiftCategories.Mesa]
        });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> publicResult = await service.GetPublicAsync(new GiftQueryParams(), CancellationToken.None);
        PagedResult<GiftResponseDto> adminResult = await service.GetAllAdminAsync(new GiftQueryParams(), CancellationToken.None);

        GiftResponseDto item = Assert.Single(publicResult.Items);
        Assert.Equal("Pratos", item.Name);
        Assert.Equal(2, adminResult.TotalCount);
    }

    [Fact]
    public async Task GetPublicAsync_DeveRetornarVazio_QuandoCategoriaSolicitadaNaoEstaHabilitada()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Panela", price: 100m, category: GiftCategories.Cozinha);
        SeedGift(context, "Pratos", price: 200m, category: GiftCategories.Mesa);
        SeedCoupleWithSiteSettings(context, new SiteSettingsDto
        {
            ShowGiftCategories = true,
            EnabledCategories = [GiftCategories.Mesa]
        });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetPublicAsync(new GiftQueryParams
        {
            Category = GiftCategories.Cozinha
        }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetPublicAsync_DeveOcultarCategoriaNoDto_QuandoShowGiftCategoriesForFalse()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Panela", price: 100m, category: GiftCategories.Cozinha);
        SeedCoupleWithSiteSettings(context, new SiteSettingsDto
        {
            ShowGiftCategories = false,
            EnabledCategories = []
        });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetPublicAsync(new GiftQueryParams(), CancellationToken.None);

        GiftResponseDto item = Assert.Single(result.Items);
        Assert.Equal(string.Empty, item.Category);
    }

    [Fact]
    public async Task GetAllAsync_DeveMarcarFullyFundedSemIndisponibilizarPresente()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, "Completo", price: 100m);
        SeedContribution(context, gift.Id, 100m);
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams(), CancellationToken.None);
        GiftResponseDto item = Assert.Single(result.Items);

        Assert.True(item.Available);
        Assert.True(item.FullyFunded);
    }

    [Fact]
    public async Task GetStatsAsync_DeveContarCompletedPorValorArrecadado()
    {
        AppDbContext context = CreateContext();
        Gift completed = SeedGift(context, "Completo", price: 100m);
        Gift unavailable = SeedGift(context, "Indisponivel", price: 100m, available: false);
        SeedContribution(context, completed.Id, 100m);
        SeedContribution(context, unavailable.Id, 50m);
        SeedContribution(context, unavailable.Id, 50m, contributorName: "Bruno", status: ContributionStatus.Pending);
        GiftService service = CreateService(context);

        GiftStatsDto stats = await service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Completed);
        Assert.Equal(1, stats.Contributors);
        Assert.Equal(150m, stats.Raised);
        Assert.Equal(200m, stats.Goal);
    }

    [Fact]
    public async Task GetStatsAsync_DeveContarContributorsDistintosApenasComContribuicoesPagas()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, "Contribuidores", price: 500m);
        SeedContribution(context, gift.Id, 100m, contributorName: " Ana ");
        SeedContribution(context, gift.Id, 50m, contributorName: "ana");
        SeedContribution(context, gift.Id, 25m, contributorName: "BRUNO");
        SeedContribution(context, gift.Id, 30m, contributorName: "Carla", status: ContributionStatus.Pending);
        SeedContribution(context, gift.Id, 40m, contributorName: "Diego", status: ContributionStatus.Cancelled);
        GiftService service = CreateService(context);

        GiftStatsDto stats = await service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, stats.Contributors);
        Assert.Equal(175m, stats.Raised);
        Assert.Equal(0, stats.Completed);
    }

    [Fact]
    public async Task ContributeAsync_DeveAceitarPresenteIndisponivel_QuandoModoPrivadoIlimitado()
    {
        AppDbContext context = CreateContext();
        Gift gift = SeedGift(context, "Indisponivel", price: 500m, available: false);
        SeedCouple(context, GiftDisplayModes.PrivateUnlimited);
        GiftService service = CreateService(context);

        ContributionResponseDto contribution = await service.ContributeAsync(gift.Id, new ContributeDto
        {
            GuestName = "Ana",
            Amount = 500m
        }, CancellationToken.None);

        Assert.Equal(gift.Id, contribution.GiftId);
        Assert.Equal(500m, contribution.Amount);
    }


    [Fact]
    public async Task GetAllAsync_DeveFiltrarCategoriasDesabilitadasNaVitrinePublica()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Casa", 100m, category: "Casa");
        SeedGift(context, "Cozinha", 100m, category: "Cozinha");
        SeedGift(context, "Sem categoria", 100m, category: string.Empty);
        SeedCouple(context, GiftDisplayModes.Traditional, new SiteSettingsDto { EnabledCategories = ["Casa"], GiftSectionTitle = "Escolha seu presente", SearchPlaceholder = "Buscar presente...", PresentButtonLabel = "Presentear", EmptyStateTitle = "Nenhum presente encontrado", EmptyStateMessage = "Tente ajustar os filtros ou buscar por outro termo" });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams(), CancellationToken.None);

        Assert.Equal(["Casa", string.Empty], result.Items.Select(x => x.Category));
    }

    [Fact]
    public async Task GetAllAdminAsync_NaoDeveFiltrarCategoriasDesabilitadas()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Casa", 100m, category: "Casa");
        SeedGift(context, "Cozinha", 100m, category: "Cozinha");
        SeedCouple(context, GiftDisplayModes.Traditional, new SiteSettingsDto { EnabledCategories = ["Casa"], GiftSectionTitle = "Escolha seu presente", SearchPlaceholder = "Buscar presente...", PresentButtonLabel = "Presentear", EmptyStateTitle = "Nenhum presente encontrado", EmptyStateMessage = "Tente ajustar os filtros ou buscar por outro termo" });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAdminAsync(new GiftQueryParams(), CancellationToken.None);

        Assert.Equal(["Casa", "Cozinha"], result.Items.Select(x => x.Category));
    }

    [Fact]
    public async Task GetAllAsync_DeveRetornarVazio_QuandoFiltroCategoriaPublicaNaoHabilitada()
    {
        AppDbContext context = CreateContext();
        SeedGift(context, "Cozinha", 100m, category: "Cozinha");
        SeedCouple(context, GiftDisplayModes.Traditional, new SiteSettingsDto { EnabledCategories = ["Casa"], GiftSectionTitle = "Escolha seu presente", SearchPlaceholder = "Buscar presente...", PresentButtonLabel = "Presentear", EmptyStateTitle = "Nenhum presente encontrado", EmptyStateMessage = "Tente ajustar os filtros ou buscar por outro termo" });
        GiftService service = CreateService(context);

        PagedResult<GiftResponseDto> result = await service.GetAllAsync(new GiftQueryParams { Category = "Cozinha" }, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GiftService CreateService(AppDbContext context)
    {
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        return new(
            new GiftRepository(context),
            new ContributionRepository(context),
            new PaymentRepository(context),
            new CoupleRepository(context),
            cache,
            new ApplicationCacheService(cache));
    }

    private static Gift SeedGift(
        AppDbContext context,
        string name,
        decimal price,
        decimal? total = null,
        bool available = true,
        string category = GiftCategories.Casa)
    {
        Gift gift = Gift.Create(name, $"{name} description", price, total ?? price, $"{name}.jpg", category, available, true);

        context.Gifts.Add(gift);
        context.SaveChanges();
        return gift;
    }

    private static void SeedContribution(
        AppDbContext context,
        Guid giftId,
        decimal amount,
        string contributorName = "Ana",
        string status = ContributionStatus.Paid)
    {
        context.Contributions.Add(Contribution.Create(giftId, contributorName, string.Empty, amount, "pix", DateTime.UtcNow, status));
        context.SaveChanges();
    }

    private static void SeedCouple(AppDbContext context, string giftDisplayMode, SiteSettingsDto? siteSettings = null)
    {
        Couple couple = Couple.Create();
        couple.Update(
            "Ana & Bruno",
            DateTime.UtcNow,
            string.Empty,
            string.Empty,
            string.Empty,
            "#C79A6D",
            "#F7F0EA",
            giftDisplayMode,
            null,
            siteSettings?.ToSiteSettingsJson());

        context.Couples.Add(couple);
        context.SaveChanges();
    }

    private static void SeedCoupleWithSiteSettings(AppDbContext context, SiteSettingsDto settings)
    {
        Couple couple = Couple.Create();
        couple.Update(
            "Ana & Bruno",
            DateTime.UtcNow,
            string.Empty,
            string.Empty,
            string.Empty,
            "#C79A6D",
            "#F7F0EA",
            GiftDisplayModes.Traditional,
            null,
            JsonSerializer.Serialize(settings));

        context.Couples.Add(couple);
        context.SaveChanges();
    }
}
