using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using Xunit;

namespace wedding.gift.Tests;

public sealed class AdministrativeOperationsTests
{
    [Fact]
    public void GiftCreateDto_DeveAceitarSomenteAsCincoCategoriasENull()
    {
        string?[] validCategories = [null, .. GiftCategories.All];
        foreach (string? category in validCategories)
        {
            GiftCreateDto dto = ValidGiftDto(category);
            Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
        }

        GiftCreateDto invalid = ValidGiftDto("cozinha");
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));
    }

    [Fact]
    public void Mensagem_DeveSerIdempotenteAoLerEArquivar()
    {
        Contribution contribution = Contribution.Create(Guid.NewGuid(), "Maria", "Mensagem", 10, "pix", DateTime.UtcNow, ContributionStatus.Paid);
        contribution.SetMessageRead(true);
        contribution.SetMessageArchived(true);
        DateTime? readAt = contribution.MessageReadAtUtc;
        DateTime? archivedAt = contribution.MessageArchivedAtUtc;

        contribution.SetMessageRead(true);
        contribution.SetMessageArchived(true);

        Assert.Equal(readAt, contribution.MessageReadAtUtc);
        Assert.Equal(archivedAt, contribution.MessageArchivedAtUtc);
    }

    [Fact]
    public async Task Contributions_DeveIsolarCasalECombinarFiltrosComPaginacao()
    {
        AppDbContext context = CreateContext();
        Guid coupleA = Guid.NewGuid();
        Guid coupleB = Guid.NewGuid();
        Gift giftA = Gift.Create("Cafeteira", "", 100, 100, "", GiftCategories.Cozinha, true, coupleA);
        Gift giftB = Gift.Create("Cafeteira", "", 100, 100, "", GiftCategories.Cozinha, true, coupleB);
        context.Gifts.AddRange(giftA, giftB);
        context.Contributions.AddRange(
            Contribution.Create(giftA.Id, "Maria", "Felicidades!", 50, "pix", DateTime.UtcNow, ContributionStatus.Paid, coupleA, Guid.NewGuid().ToString(), "maria@example.com"),
            Contribution.Create(giftA.Id, "Outra", "", 20, "credit_card", DateTime.UtcNow, ContributionStatus.Paid, coupleA),
            Contribution.Create(giftB.Id, "Maria", "Felicidades!", 50, "pix", DateTime.UtcNow, ContributionStatus.Paid, coupleB));
        await context.SaveChangesAsync();
        ContributionService service = CreateContributionService(context, new FakeRequestContext(coupleA));

        PagedResult<ContributionResponseDto> result = await service.GetAllAdminAsync(new ContributionAdminQueryParams
        {
            Search = "Maria",
            GiftId = giftA.Id,
            Status = ContributionStatus.Paid,
            PaymentMethod = "Pix",
            HasMessage = true,
            Page = 1,
            PageSize = 1
        }, CancellationToken.None);

        ContributionResponseDto item = Assert.Single(result.Items);
        Assert.Equal(coupleA, context.Contributions.Single(x => x.Id == item.Id).CoupleId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task OrderLookup_DeveAceitarRespostaNeutraELimitarPorEmail()
    {
        AppDbContext context = CreateContext();
        FakeRequestContext requestContext = new(Couple.SingletonId);
        OrderLookupService service = CreateLookupService(context, requestContext);
        OrderLookupRequestDto request = new() { Email = "inexistente@example.com", OrderId = Guid.NewGuid().ToString() };

        for (int i = 0; i < 5; i++)
            await service.RequestAsync(request, CancellationToken.None);

        Assert.Empty(context.EmailOutboxMessages);
        await Assert.ThrowsAsync<TooManyRequestsException>(() => service.RequestAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task OrderLookup_DeveConsumirUmaVezERejeitarExpiradoOuInexistente()
    {
        AppDbContext context = CreateContext();
        Gift gift = Gift.Create("Air Fryer", "", 500, 500, "image.jpg", null!, true);
        Payment payment = Payment.CreatePix(gift.Id, gift.Name, "Maria", "", "maria@example.com", "CPF", "", Guid.NewGuid().ToString(), 250, "approved", null, "MP1", "PAY1", "", null);
        context.AddRange(gift, payment);
        await context.SaveChangesAsync();
        OrderLookupService service = CreateLookupService(context, new FakeRequestContext(Couple.SingletonId));
        string token = await service.CreateTokenAsync(payment.Id, CancellationToken.None);

        OrderLookupResponseDto result = await service.ConsumeAsync(token, CancellationToken.None);
        Assert.Equal(payment.OrderId, result.OrderId);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ConsumeAsync(token, CancellationToken.None));

        context.PaymentOrderLookupTokens.Add(PaymentOrderLookupToken.Create(payment.Id, Hash("expired"), DateTime.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => service.ConsumeAsync("expired", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ConsumeAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task ExportCsv_DeveGerarBomEAcentos()
    {
        AppDbContext context = CreateContext();
        Gift gift = Gift.Create("Eletrodomestico", "", 100, 100, "", GiftCategories.Eletrodomesticos, true);
        context.Gifts.Add(gift);
        context.Contributions.Add(Contribution.Create(gift.Id, "Joao Acentuado", "Felicidades", 25.5m, "pix", DateTime.UtcNow, ContributionStatus.Paid, gift.CoupleId, "PEDIDO", "joao@example.com"));
        await context.SaveChangesAsync();
        ContributionService service = CreateContributionService(context, new FakeRequestContext(Couple.SingletonId));

        byte[] bytes = await service.ExportCsvAsync(new ContributionAdminQueryParams(), CancellationToken.None);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        string csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains(GiftCategories.Eletrodomesticos, csv);
        Assert.Contains("25,50", csv);
    }

    [Fact]
    public async Task Overview_DeveExcluirPagamentoNaoAprovado()
    {
        AppDbContext context = CreateContext();
        Gift gift = Gift.Create("Presente", "", 200, 200, "", null!, true);
        Contribution approvedContribution = Contribution.Create(gift.Id, "Maria", "", 100, "pix", DateTime.UtcNow, ContributionStatus.Paid);
        Payment approved = Payment.CreatePix(gift.Id, gift.Name, "Maria", "", "maria@example.com", "CPF", "", Guid.NewGuid().ToString(), 100, "approved", null, "MPA", "PAYA", "", null);
        approved.MarkContributionCreated(approvedContribution.Id);
        Contribution pendingContribution = Contribution.Create(gift.Id, "Ana", "", 80, "pix", DateTime.UtcNow, ContributionStatus.Paid);
        Payment pending = Payment.CreatePix(gift.Id, gift.Name, "Ana", "", "ana@example.com", "CPF", "", Guid.NewGuid().ToString(), 80, "pending", null, "MPP", "PAYP", "", null);
        pending.MarkContributionCreated(pendingContribution.Id);
        context.AddRange(gift, approvedContribution, pendingContribution, approved, pending);
        await context.SaveChangesAsync();
        CoupleOverviewService service = new(new GiftRepository(context), new ContributionRepository(context), new PaymentRepository(context), new FakeRequestContext(Couple.SingletonId));

        CoupleOverviewDto overview = await service.GetAsync(30, CancellationToken.None);
        Assert.Equal(100m, overview.TotalRaised);
        Assert.Equal(1, overview.ApprovedContributions);
    }

    [Fact]
    public async Task AprovacaoRepetida_DeveCriarContribuicaoEOutboxUmaUnicaVez()
    {
        AppDbContext context = CreateContext();
        Gift gift = Gift.Create("Presente", "", 200, 200, "", GiftCategories.Casa, true);
        context.Gifts.Add(gift);
        await context.SaveChangesAsync();
        FakeMercadoPago mercadoPago = new();
        PaymentService service = new(
            mercadoPago, new PaymentRepository(context), new GiftRepository(context), new ContributionRepository(context),
            new CoupleRepository(context), new FakeEmail(), new ApplicationCacheService(new MemoryCache(new MemoryCacheOptions())),
            NullLogger<PaymentService>.Instance, null, new FakeRequestContext(Couple.SingletonId), new OperationalRepository(context));
        CardPaymentRequestDto request = new()
        {
            GiftId = gift.Id,
            ContributorName = "Maria",
            CardToken = "token",
            OrderId = Guid.NewGuid().ToString(),
            Amount = 100,
            Installments = 1,
            Method = "credit_card",
            PaymentMethodId = "visa",
            PayerEmail = "maria@example.com",
            PayerDocNumber = "12345678909"
        };

        await service.ProcessCardPaymentAsync(request, CancellationToken.None);
        await service.ProcessCardPaymentAsync(request, CancellationToken.None);

        Assert.Single(context.Contributions);
        Assert.Single(context.EmailOutboxMessages);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ContributionService CreateContributionService(AppDbContext context, IRequestContext requestContext)
        => new(new ContributionRepository(context), new GiftRepository(context), new PaymentRepository(context), new CoupleRepository(context),
            new ApplicationCacheService(new MemoryCache(new MemoryCacheOptions())), requestContext, new OperationalRepository(context));

    private static OrderLookupService CreateLookupService(AppDbContext context, IRequestContext requestContext)
        => new(new PaymentRepository(context), new GiftRepository(context), new CoupleRepository(context), new OperationalRepository(context), requestContext);

    private static GiftCreateDto ValidGiftDto(string? category) => new() { Name = "Presente", Price = 100, Total = 100, Category = category };
    private static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class FakeRequestContext(Guid coupleId) : IRequestContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? CoupleId { get; } = coupleId;
        public bool IsSuperAdmin { get; init; }
        public string CorrelationId { get; } = Guid.NewGuid().ToString();
        public string RemoteIpAddress { get; } = "127.0.0.1";
    }

    private sealed class FakeMercadoPago : IMercadoPagoService
    {
        public Task<PaymentResponseDto> CreateCardOrderAsync(CardPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Approved, MpOrderId = "MP-APPROVED", MpPaymentId = "PAY-APPROVED" });
        public Task<PaymentResponseDto> CreatePixOrderAsync(PixPaymentRequestDto request, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Pending, MpOrderId = "MP-PENDING" });
        public Task<PaymentResponseDto> GetOrderStatusAsync(string mpOrderId, CancellationToken cancellationToken)
            => Task.FromResult(new PaymentResponseDto { Status = PaymentStatuses.Approved, MpOrderId = mpOrderId });
    }

    private sealed class FakeEmail : IEmailService
    {
        public Task SendErrorNotificationAsync(string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendEmailConfirmationAsync(string toEmail, string toName, string token, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendContributionNotificationAsync(string contributorName, decimal amount, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SendPaymentAttemptNotificationAsync(string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
