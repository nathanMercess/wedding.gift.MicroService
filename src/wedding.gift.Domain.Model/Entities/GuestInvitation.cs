namespace wedding.gift.Domain.Model.Entities;

public sealed class GuestInvitation
{
    private GuestInvitation()
    {
    }

    public Guid Id { get; private init; }
    public Guid CoupleId { get; private init; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private init; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static GuestInvitation Create(Guid coupleId, string name, string normalizedName)
    {
        DateTime now = DateTime.UtcNow;

        return new GuestInvitation
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void Update(string name, string normalizedName)
    {
        Name = name.Trim();
        NormalizedName = normalizedName;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
