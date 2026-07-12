using System.ComponentModel.DataAnnotations;

namespace wedding.gift.Crosscutting.Models.DTOs.Auth;

public sealed class UserRoleUpdateDto
{
    [Required]
    [RegularExpression("^(Admin|SuperAdmin|Member)$")]
    public string Role { get; set; } = string.Empty;
}
