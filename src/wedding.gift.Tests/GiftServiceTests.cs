using Microsoft.EntityFrameworkCore;
using Xunit;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Services.Implementations;

namespace wedding.gift.Tests;

public class GiftServiceTests
{
    [Fact]
    public async Task CreateAsync_DeveAceitarCategoriaOpcional_QuandoNaoInformada()
    {
        var context = CreateContext();
        var service = new GiftService(context);

        var created = await service.CreateAsync(new GiftCreateDto
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
        var context = CreateContext();
        SeedGift(context, "Dez", price: 10m);
        SeedGift(context, "Dois", price: 2m);
        SeedGift(context, "Cem", price: 100.50m);
        var service = new GiftService(context);

        var result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Price,
            OrderDir = SortDirection.Asc
        }, CancellationToken.None);

        Assert.Equal([2m, 10m, 100.50m], result.Items.Select(x => x.Price));
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarPorTotalComoDecimal()
    {
        var context = CreateContext();
        SeedGift(context, "Dez", price: 10m, total: 10m);
        SeedGift(context, "Dois", price: 2m, total: 2m);
        SeedGift(context, "Cem", price: 100.50m, total: 100.50m);
        var service = new GiftService(context);

        var result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Total,
            OrderDir = SortDirection.Desc
        }, CancellationToken.None);

        Assert.Equal([100.50m, 10m, 2m], result.Items.Select(x => x.Total));
    }

    [Fact]
    public async Task GetAllAsync_DeveOrdenarPorValorArrecadadoComoDecimal()
    {
        var context = CreateContext();
        var ten = SeedGift(context, "Dez", price: 10m);
        var two = SeedGift(context, "Dois", price: 2m);
        var hundred = SeedGift(context, "Cem", price: 100.50m);
        SeedContribution(context, ten.Id, 10m);
        SeedContribution(context, two.Id, 2m);
        SeedContribution(context, hundred.Id, 100.50m);
        var service = new GiftService(context);

        var result = await service.GetAllAsync(new GiftQueryParams
        {
            OrderBy = GiftSortField.Raised,
            OrderDir = SortDirection.Asc
        }, CancellationToken.None);

        Assert.Equal([2m, 10m, 100.50m], result.Items.Select(x => x.Raised));
    }

    [Fact]
    public async Task GetAllAsync_DeveMarcarFullyFundedSemIndisponibilizarPresente()
    {
        var context = CreateContext();
        var gift = SeedGift(context, "Completo", price: 100m);
        SeedContribution(context, gift.Id, 100m);
        var service = new GiftService(context);

        var result = await service.GetAllAsync(new GiftQueryParams(), CancellationToken.None);
        var item = Assert.Single(result.Items);

        Assert.True(item.Available);
        Assert.True(item.FullyFunded);
    }

    [Fact]
    public async Task GetStatsAsync_DeveContarCompletedPorValorArrecadado()
    {
        var context = CreateContext();
        var completed = SeedGift(context, "Completo", price: 100m);
        var unavailable = SeedGift(context, "Indisponivel", price: 100m, available: false);
        SeedContribution(context, completed.Id, 100m);
        SeedContribution(context, unavailable.Id, 50m);
        SeedContribution(context, unavailable.Id, 50m, contributorName: "Bruno", status: ContributionStatus.Pending);
        var service = new GiftService(context);

        var stats = await service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, stats.Total);
        Assert.Equal(1, stats.Completed);
        Assert.Equal(1, stats.Contributors);
        Assert.Equal(150m, stats.Raised);
        Assert.Equal(200m, stats.Goal);
    }

    [Fact]
    public async Task GetStatsAsync_DeveContarContributorsDistintosApenasComContribuicoesPagas()
    {
        var context = CreateContext();
        var gift = SeedGift(context, "Contribuidores", price: 500m);
        SeedContribution(context, gift.Id, 100m, contributorName: " Ana ");
        SeedContribution(context, gift.Id, 50m, contributorName: "ana");
        SeedContribution(context, gift.Id, 25m, contributorName: "BRUNO");
        SeedContribution(context, gift.Id, 30m, contributorName: "Carla", status: ContributionStatus.Pending);
        SeedContribution(context, gift.Id, 40m, contributorName: "Diego", status: ContributionStatus.Cancelled);
        var service = new GiftService(context);

        var stats = await service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, stats.Contributors);
        Assert.Equal(175m, stats.Raised);
        Assert.Equal(0, stats.Completed);
    }

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Gift SeedGift(AppDbContext context, string name, decimal price, decimal? total = null, bool available = true)
    {
        var gift = new Gift
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"{name} description",
            Price = price,
            Total = total ?? price,
            Image = $"{name}.jpg",
            Category = "Casa",
            Available = available,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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
        context.Contributions.Add(new Contribution
        {
            Id = Guid.NewGuid(),
            GiftId = giftId,
            ContributorName = contributorName,
            Amount = amount,
            PaymentMethod = "pix",
            PaidAt = DateTime.UtcNow,
            Status = status
        });

        context.SaveChanges();
    }
}
