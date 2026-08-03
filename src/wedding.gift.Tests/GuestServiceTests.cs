using Microsoft.EntityFrameworkCore;
using System.Text;
using wedding.gift.Crosscutting.Constants;
using wedding.gift.Crosscutting.Models.DTOs;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Implementations.DataContext;
using wedding.gift.Infra.Implementations.Repositories;
using wedding.gift.Services.Contracts;
using wedding.gift.Services.Implementations;
using wedding.gift.Services.Implementations.Exceptions;
using wedding.gift.Services.Implementations.Extensions;
using Xunit;

namespace wedding.gift.Tests;

public sealed class GuestServiceTests
{
    [Fact]
    public async Task CreateConfirmationAsync_ShouldCreateIndividualLinkedAndFreeTextGuests()
    {
        AppDbContext context = CreateContext();
        GuestInvitation submitterInvitation = SeedInvitation(context, "Mariana Silva");
        GuestInvitation companionInvitation = SeedInvitation(context, "Ana Souza");
        GuestService service = CreateService(context);

        GuestConfirmationResponseDto result = await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests =
            [
                Guest(submitterInvitation.Id, "Mariana Silva", true),
                Guest(null, "João Silva"),
                Guest(companionInvitation.Id, "Ana Souza")
            ]
        }, CancellationToken.None);

        Assert.Equal("Mariana Silva", result.SubmittedByName);
        Assert.Equal(3, result.PartySize);
        Assert.Equal(2, result.Guests.Count(x => x.Source == GuestConfirmationSources.RegisteredList));
        Assert.Single(result.Guests, x => x.Source == GuestConfirmationSources.FreeText);
        Assert.Equal(submitterInvitation.Id, result.Guests.Single(x => x.IsSubmitter).GuestInvitationId);
        Assert.Equal(companionInvitation.Id, result.Guests.Single(x => x.Name == "Ana Souza").GuestInvitationId);
        Assert.All(result.Guests, x => Assert.Equal(DateTimeKind.Utc, x.CreatedAtUtc.Kind));
        Assert.Equal(DateTimeKind.Utc, result.ConfirmedAtUtc.Kind);
        Assert.Single(context.GuestConfirmations);
        Assert.Equal(3, context.ConfirmedGuests.Count());
    }

    [Fact]
    public async Task CreateConfirmationAsync_ShouldKeepTypedNameAsFreeTextEvenWhenInvitationExists()
    {
        AppDbContext context = CreateContext();
        GuestInvitation invitation = SeedInvitation(context, "João da Silva");
        GuestService service = CreateService(context);

        GuestConfirmationResponseDto result = await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(null, "João da Silva", true)]
        }, CancellationToken.None);

        ConfirmedGuestResponseDto guest = Assert.Single(result.Guests);
        Assert.Null(guest.GuestInvitationId);
        Assert.Equal(GuestConfirmationSources.FreeText, guest.Source);
        GuestSuggestionDto suggestion = Assert.Single(await service.GetSuggestionsAsync("joao", CancellationToken.None));
        Assert.Equal(invitation.Id, suggestion.Id);
    }

    [Fact]
    public async Task CreateConfirmationAsync_ShouldRejectInvalidGroupWithoutPersistingAnything()
    {
        AppDbContext context = CreateContext();
        GuestService service = CreateService(context);

        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(null, "José", true), Guest(null, " jose ")]
        }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(null, "A", true), Guest(null, "B", true)]
        }, CancellationToken.None));
        await Assert.ThrowsAsync<BadRequestException>(() => service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = Enumerable.Range(1, 21).Select(x => Guest(null, $"Pessoa {x}", x == 1)).ToList()
        }, CancellationToken.None));

        Assert.Empty(context.GuestConfirmations);
        Assert.Empty(context.ConfirmedGuests);
    }

    [Fact]
    public async Task CreateConfirmationAsync_ShouldRejectInvalidInvitationAtomically()
    {
        AppDbContext context = CreateContext();
        GuestInvitation invitation = SeedInvitation(context, "Mariana Silva");
        GuestService service = CreateService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(invitation.Id, invitation.Name, true), Guest(Guid.NewGuid(), "Inválido")]
        }, CancellationToken.None));

        Assert.Empty(context.GuestConfirmations);
        Assert.Empty(context.ConfirmedGuests);
    }

    [Fact]
    public async Task GetSuggestionsAsync_ShouldExcludeEveryLinkedGuestInactiveAndOtherCoupleInvitations()
    {
        AppDbContext context = CreateContext();
        GuestInvitation available = SeedInvitation(context, "Família Santos");
        GuestInvitation answeredSubmitter = SeedInvitation(context, "Família Santana");
        GuestInvitation answeredCompanion = SeedInvitation(context, "Família Sandoval");
        GuestInvitation inactive = SeedInvitation(context, "Família Santiago");
        inactive.SetActive(false);
        SeedInvitation(context, "Família Sanches", Guid.NewGuid());
        context.SaveChanges();
        GuestService service = CreateService(context);
        await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests =
            [
                Guest(answeredSubmitter.Id, answeredSubmitter.Name, true),
                Guest(answeredCompanion.Id, answeredCompanion.Name)
            ]
        }, CancellationToken.None);

        GuestSuggestionDto item = Assert.Single(await service.GetSuggestionsAsync("fam", CancellationToken.None));

        Assert.Equal(available.Id, item.Id);
    }

    [Fact]
    public async Task DeleteConfirmationAsync_ShouldReleaseEveryLinkedInvitation()
    {
        AppDbContext context = CreateContext();
        GuestInvitation submitter = SeedInvitation(context, "Mariana Silva");
        GuestInvitation companion = SeedInvitation(context, "Marina Souza");
        GuestService service = CreateService(context);
        GuestConfirmationResponseDto confirmation = await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(submitter.Id, submitter.Name, true), Guest(companion.Id, companion.Name)]
        }, CancellationToken.None);

        await service.DeleteConfirmationAsync(confirmation.Id, CancellationToken.None);

        IReadOnlyList<GuestSuggestionDto> suggestions = await service.GetSuggestionsAsync("mari", CancellationToken.None);
        Assert.Equal(2, suggestions.Count);
        Assert.Contains(suggestions, x => x.Id == submitter.Id);
        Assert.Contains(suggestions, x => x.Id == companion.Id);
    }

    [Fact]
    public async Task UpdateConfirmationAsync_ShouldReleaseRemovedInvitationAndKeepConfirmationDate()
    {
        AppDbContext context = CreateContext();
        GuestInvitation submitter = SeedInvitation(context, "Carlos Lima");
        GuestInvitation removedCompanion = SeedInvitation(context, "Laura Lima");
        GuestService service = CreateService(context);
        GuestConfirmationResponseDto created = await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(submitter.Id, submitter.Name, true), Guest(removedCompanion.Id, removedCompanion.Name)]
        }, CancellationToken.None);

        GuestConfirmationResponseDto updated = await service.UpdateConfirmationAsync(created.Id, new GuestConfirmationCreateDto
        {
            Guests = [Guest(submitter.Id, submitter.Name, true), Guest(null, "Paulo Lima")]
        }, CancellationToken.None);

        Assert.Equal(created.ConfirmedAtUtc, updated.ConfirmedAtUtc);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);
        Assert.Equal(2, updated.PartySize);
        GuestSuggestionDto released = Assert.Single(await service.GetSuggestionsAsync("laur", CancellationToken.None));
        Assert.Equal(removedCompanion.Id, released.Id);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCountEveryFreeTextPerson()
    {
        AppDbContext context = CreateContext();
        GuestService service = CreateService(context);
        await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(null, "Responsável", true), Guest(null, "Acompanhante")]
        }, CancellationToken.None);

        GuestSummaryDto summary = await service.GetSummaryAsync(CancellationToken.None);

        Assert.Equal(1, summary.ConfirmationCount);
        Assert.Equal(2, summary.ConfirmedGuestCount);
        Assert.Equal(2, summary.FreeTextGuestCount);
    }

    [Fact]
    public async Task ExportConfirmationsCsvAsync_ShouldExportEveryGuestWithItsSource()
    {
        AppDbContext context = CreateContext();
        GuestInvitation invitation = SeedInvitation(context, "Mariana Silva");
        GuestService service = CreateService(context);
        await service.CreateConfirmationAsync(new GuestConfirmationCreateDto
        {
            Guests = [Guest(invitation.Id, invitation.Name, true), Guest(null, "João Silva")]
        }, CancellationToken.None);

        byte[] content = await service.ExportConfirmationsCsvAsync(new GuestConfirmationQueryDto(), CancellationToken.None);
        string csv = Encoding.UTF8.GetString(content);

        Assert.Contains("Responsável;Convidado;Origem", csv);
        Assert.Contains("\"Mariana Silva\";\"Mariana Silva\";\"RegisteredList\";Sim", csv);
        Assert.Contains("\"Mariana Silva\";\"João Silva\";\"FreeText\";Não", csv);
    }

    [Fact]
    public async Task ImportInvitationsAsync_ShouldBeIdempotentAndScopedByCouple()
    {
        AppDbContext context = CreateContext();
        Guid coupleA = Guid.NewGuid();
        Guid coupleB = Guid.NewGuid();
        SeedInvitation(context, "Família Silva", coupleB);
        GuestService service = CreateService(context, coupleA);
        byte[] csv = Encoding.UTF8.GetBytes("Nome\r\nFamília Silva\r\n\r\n familia  silva \r\nAna Souza\r\n");

        GuestInvitationImportResponseDto first = await service.ImportInvitationsAsync(new MemoryStream(csv), CancellationToken.None);
        GuestInvitationImportResponseDto second = await service.ImportInvitationsAsync(new MemoryStream(csv), CancellationToken.None);

        Assert.Equal(2, first.CreatedCount);
        Assert.Equal(2, first.SkippedCount);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(4, second.SkippedCount);
        Assert.Equal(2, context.GuestInvitations.Count(x => x.CoupleId == coupleA));
        Assert.Single(context.GuestInvitations.Where(x => x.CoupleId == coupleB));
    }

    [Fact]
    public async Task AdministrativeChanges_ShouldRespectCoupleScopeAndWriteAuditLog()
    {
        AppDbContext context = CreateContext();
        Guid coupleA = Guid.NewGuid();
        Guid coupleB = Guid.NewGuid();
        GuestInvitation otherCoupleInvitation = SeedInvitation(context, "Outro casal", coupleB);
        GuestService service = CreateService(context, coupleA, true);

        GuestInvitationResponseDto created = await service.CreateInvitationAsync(
            new GuestInvitationCreateDto { Name = "Família A" },
            CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateInvitationAsync(
            otherCoupleInvitation.Id,
            new GuestInvitationUpdateDto { Name = "Inválido" },
            CancellationToken.None));
        Assert.Contains(context.AuditLogs, x => x.EntityId == created.Id.ToString() && x.CoupleId == coupleA);
    }

    private static GuestConfirmationGuestDto Guest(Guid? invitationId, string name, bool isSubmitter = false)
        => new() { GuestInvitationId = invitationId, Name = name, IsSubmitter = isSubmitter };

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static GuestService CreateService(
        AppDbContext context,
        Guid? coupleId = null,
        bool audit = false)
    {
        FakeRequestContext requestContext = new(coupleId ?? Couple.SingletonId);
        return new GuestService(
            new GuestRepository(context),
            requestContext,
            audit ? new OperationalRepository(context) : null);
    }

    private static GuestInvitation SeedInvitation(
        AppDbContext context,
        string name,
        Guid? coupleId = null)
    {
        GuestInvitation invitation = GuestInvitation.Create(
            coupleId ?? Couple.SingletonId,
            name,
            GuestNameNormalizer.Normalize(name));
        context.GuestInvitations.Add(invitation);
        context.SaveChanges();
        return invitation;
    }

    private sealed class FakeRequestContext(Guid coupleId) : IRequestContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? CoupleId { get; } = coupleId;
        public bool IsSuperAdmin => false;
        public string CorrelationId { get; } = Guid.NewGuid().ToString();
        public string RemoteIpAddress => "127.0.0.1";
    }
}
