namespace wedding.gift.Domain.Model.Entities;

public sealed class GuestConfirmation
{
    private readonly List<ConfirmedGuest> _guests = [];

    private GuestConfirmation()
    {
    }

    public Guid Id { get; private init; }
    public Guid CoupleId { get; private init; }
    public DateTime ConfirmedAtUtc { get; private init; }
    public DateTime UpdatedAtUtc { get; private set; }
    public ICollection<ConfirmedGuest> Guests => _guests;

    public static GuestConfirmation Create(Guid coupleId)
    {
        DateTime now = DateTime.UtcNow;

        return new GuestConfirmation
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId,
            ConfirmedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void AddGuest(ConfirmedGuest guest)
        => _guests.Add(guest);

    public void ReplaceGuests(IEnumerable<ConfirmedGuest> guests)
    {
        List<ConfirmedGuest> replacements = guests.ToList();
        HashSet<Guid> replacementIds = replacements.Select(x => x.Id).ToHashSet();

        _guests.RemoveAll(x => !replacementIds.Contains(x.Id));
        foreach (ConfirmedGuest replacement in replacements)
        {
            if (_guests.All(x => x.Id != replacement.Id))
                _guests.Add(replacement);
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }
}
