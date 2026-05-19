namespace wedding.gift.Crosscutting.Models.DTOs;

public class AuthUserResponseDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsEmailConfirmed { get; set; }
}
