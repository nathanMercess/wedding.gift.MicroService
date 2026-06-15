namespace wedding.gift.Crosscutting.Models.Configurations;

public class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = ["http://localhost:4200"];
}
