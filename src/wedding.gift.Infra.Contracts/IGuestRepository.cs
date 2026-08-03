using wedding.gift.Domain.Model.Entities;

namespace wedding.gift.Infra.Contracts;

public interface IGuestRepository
{
    IQueryable<GuestInvitation> QueryInvitations();
    IQueryable<GuestConfirmation> QueryConfirmations();
    IQueryable<ConfirmedGuest> QueryConfirmedGuests();
    Task<GuestInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<GuestConfirmation?> GetConfirmationByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddInvitationAsync(GuestInvitation invitation, CancellationToken cancellationToken);
    Task AddInvitationsAsync(IEnumerable<GuestInvitation> invitations, CancellationToken cancellationToken);
    Task AddConfirmationAsync(GuestConfirmation confirmation, CancellationToken cancellationToken);
    Task AddConfirmedGuestsAsync(IEnumerable<ConfirmedGuest> guests, CancellationToken cancellationToken);
    void RemoveInvitation(GuestInvitation invitation);
    void RemoveConfirmedGuests(IEnumerable<ConfirmedGuest> guests);
    void RemoveConfirmation(GuestConfirmation confirmation);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
