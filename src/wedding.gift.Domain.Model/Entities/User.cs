namespace wedding.gift.Domain.Model.Entities;

public sealed class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public Guid? CoupleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string PasswordSalt { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public bool IsEmailConfirmed { get; private set; }
    public string? EmailConfirmationToken { get; private set; }
    public DateTime? EmailConfirmationTokenExpiresAt { get; private set; }
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetTokenExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static User Create(
        string name,
        string email,
        string normalizedEmail,
        string passwordHash,
        string passwordSalt,
        string role,
        bool isEmailConfirmed,
        string? emailConfirmationToken,
        DateTime? emailConfirmationTokenExpiresAt,
        Guid? coupleId = null)
    {
        DateTime now = DateTime.UtcNow;

        return new User
        {
            Id = Guid.NewGuid(),
            CoupleId = coupleId ?? (role == "SuperAdmin" ? null : Couple.SingletonId),
            Name = name.Trim(),
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail.Trim(),
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            Role = role,
            IsActive = true,
            IsEmailConfirmed = isEmailConfirmed,
            EmailConfirmationToken = emailConfirmationToken,
            EmailConfirmationTokenExpiresAt = emailConfirmationTokenExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void ConfirmEmail()
    {
        IsEmailConfirmed = true;
        EmailConfirmationToken = null;
        EmailConfirmationTokenExpiresAt = null;
        Touch();
    }

    public void SetEmailConfirmationToken(string tokenHash, DateTime expiresAtUtc)
    {
        EmailConfirmationToken = tokenHash;
        EmailConfirmationTokenExpiresAt = expiresAtUtc;
        Touch();
    }

    public void SetPasswordResetToken(string tokenHash, DateTime expiresAtUtc)
    {
        PasswordResetToken = tokenHash;
        PasswordResetTokenExpiresAt = expiresAtUtc;
        Touch();
    }

    public void ResetPassword(string passwordHash, string passwordSalt)
    {
        PasswordHash = passwordHash;
        PasswordSalt = passwordSalt;
        PasswordResetToken = null;
        PasswordResetTokenExpiresAt = null;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void SetRole(string role)
    {
        Role = role;
        Touch();
    }

    private void Touch()
        => UpdatedAt = DateTime.UtcNow;
}
