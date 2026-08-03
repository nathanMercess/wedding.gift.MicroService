namespace wedding.gift.Domain.Model.Entities;

public sealed class ConfirmedGuest
{
    private ConfirmedGuest()
    {
    }

    public Guid Id { get; private init; }
    public Guid GuestConfirmationId { get; private init; }
    public Guid? GuestInvitationId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public bool IsSubmitter { get; private set; }
    public DateTime CreatedAtUtc { get; private init; }
    public GuestConfirmation GuestConfirmation { get; private set; } = null!;
    public GuestInvitation? GuestInvitation { get; private set; }

    public static ConfirmedGuest Create(
        Guid guestConfirmationId,
        Guid? guestInvitationId,
        string name,
        string normalizedName,
        string source,
        bool isSubmitter)
        => new()
        {
            Id = Guid.NewGuid(),
            GuestConfirmationId = guestConfirmationId,
            GuestInvitationId = guestInvitationId,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            Source = source,
            IsSubmitter = isSubmitter,
            CreatedAtUtc = DateTime.UtcNow
        };

    public void Update(string name, string normalizedName, string source, bool isSubmitter)
    {
        Name = name.Trim();
        NormalizedName = normalizedName;
        Source = source;
        IsSubmitter = isSubmitter;
    }
}
