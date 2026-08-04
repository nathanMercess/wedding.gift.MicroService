using Microsoft.EntityFrameworkCore;
using wedding.gift.Domain.Model.Entities;
using wedding.gift.Infra.Contracts;
using wedding.gift.Infra.Implementations.DataContext;

namespace wedding.gift.Infra.Implementations.Repositories;

public sealed class GuestRepository(AppDbContext context) : IGuestRepository
{
    public IQueryable<GuestInvitation> QueryInvitations()
        => context.GuestInvitations.AsNoTracking();

    public IQueryable<GuestConfirmation> QueryConfirmations()
        => context.GuestConfirmations
            .AsNoTracking()
            .Include(x => x.Guests)
            .ThenInclude(x => x.GuestInvitation);

    public IQueryable<ConfirmedGuest> QueryConfirmedGuests()
        => context.ConfirmedGuests
            .AsNoTracking()
            .Include(x => x.GuestInvitation)
            .Include(x => x.GuestConfirmation);

    public async Task<GuestInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.GuestInvitations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<GuestConfirmation?> GetConfirmationByIdAsync(Guid id, CancellationToken cancellationToken)
        => await context.GuestConfirmations
            .Include(x => x.Guests)
            .ThenInclude(x => x.GuestInvitation)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddInvitationAsync(GuestInvitation invitation, CancellationToken cancellationToken)
        => await context.GuestInvitations.AddAsync(invitation, cancellationToken);

    public async Task AddInvitationsAsync(IEnumerable<GuestInvitation> invitations, CancellationToken cancellationToken)
        => await context.GuestInvitations.AddRangeAsync(invitations, cancellationToken);

    public async Task AddConfirmationAsync(GuestConfirmation confirmation, CancellationToken cancellationToken)
        => await context.GuestConfirmations.AddAsync(confirmation, cancellationToken);

    public async Task AddConfirmedGuestsAsync(IEnumerable<ConfirmedGuest> guests, CancellationToken cancellationToken)
        => await context.ConfirmedGuests.AddRangeAsync(guests, cancellationToken);

    public void RemoveInvitation(GuestInvitation invitation)
        => context.GuestInvitations.Remove(invitation);

    public void RemoveConfirmedGuests(IEnumerable<ConfirmedGuest> guests)
        => context.ConfirmedGuests.RemoveRange(guests);

    public void RemoveConfirmation(GuestConfirmation confirmation)
        => context.GuestConfirmations.Remove(confirmation);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
        => await context.SaveChangesAsync(cancellationToken);
}
