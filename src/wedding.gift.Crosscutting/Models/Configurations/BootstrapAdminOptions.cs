namespace wedding.gift.Crosscutting.Models.Configurations;

public class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public bool Enabled { get; set; } = false;
    public string Name { get; set; } = "Administrador";
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
