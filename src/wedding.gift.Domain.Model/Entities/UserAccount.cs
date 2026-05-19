namespace wedding.gift.Domain.Model.Entities;

public class UserAccount
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool IsEmailConfirmed { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public string EmailConfirmationTokenHash { get; set; } = string.Empty;
    public DateTime? EmailConfirmationTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
